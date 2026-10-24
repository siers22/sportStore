"""Integration checks using a disposable copy of a legacy SQLite database.

Build first, then run: python3 tests/verify_flows.py --dotnet /path/to/dotnet
Requires the .NET 8 runtime (or DOTNET_ROLL_FORWARD=Major for a newer runtime).
No third-party Python packages are needed. Original application data is never changed.
"""
import argparse
import base64
import concurrent.futures
import decimal
import html
import http.cookiejar
import os
from pathlib import Path
import re
import socket
import sqlite3
import subprocess
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet', default='dotnet')
parser.add_argument('--fresh', action='store_true', help='Use a new database instead of copying the legacy database')
args = parser.parse_args()


def check(condition, description):
    if not condition:
        raise AssertionError(description)
    print('PASS:', description, flush=True)


class Client:
    def __init__(self, base):
        self.base = base
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

    def request(self, path, fields=None, file=None):
        headers = {}
        data = None
        if fields is not None:
            if file:
                boundary = 'SportStoreTest' + uuid.uuid4().hex
                parts = []
                for key, value in fields.items():
                    parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'.encode())
                name, content, mime = file
                parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="Photo"; filename="{name}"\r\nContent-Type: {mime}\r\n\r\n'.encode() + content + b'\r\n')
                parts.append(f'--{boundary}--\r\n'.encode())
                data = b''.join(parts)
                headers['Content-Type'] = 'multipart/form-data; boundary=' + boundary
            else:
                data = urllib.parse.urlencode(fields).encode()
        try:
            response = self.opener.open(urllib.request.Request(self.base + path, data=data, headers=headers), timeout=20)
        except urllib.error.HTTPError as error:
            response = error
        body = response.read()
        return response.status, urllib.parse.urlsplit(response.url).path, body

    def form(self, path):
        status, _, body = self.request(path)
        assert status == 200, (path, status)
        text = body.decode()
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', text).group(1)
        return html.unescape(token), text

    def login(self, login, password):
        token, _ = self.form('/Account/Login')
        status, path, _ = self.request('/Account/Login', {'Login': login, 'Password': password, '__RequestVerificationToken': token})
        assert status == 200 and path == '/', (status, path)


with tempfile.TemporaryDirectory(prefix='sportstore-check-') as temp:
    data_dir = Path(temp)
    db_path = data_dir / 'sportstore.db'
    # Use a real legacy schema, when available, to exercise the compatibility upgrade.
    legacy = ROOT / 'SportStore/bin/Debug/net8.0/sportstore.db'
    baseline = None
    original_demo_ids = {}
    if legacy.exists() and not args.fresh:
        with sqlite3.connect(f'file:{legacy}?mode=ro&immutable=1', uri=True) as old, sqlite3.connect(db_path) as db:
            old.backup(db)
            baseline = {table: db.execute(f'SELECT COUNT(*) FROM {table}').fetchone()[0]
                        for table in ['AspNetUsers', 'Products', 'Purchases']}
            original_demo_ids = dict(db.execute("SELECT Email, Id FROM AspNetUsers WHERE Email IN ('admin@sport.local', 'buyer@sport.local', 'trail@sport.local', 'arena@sport.local')"))
            if 'ImagePath' in [row[1] for row in db.execute('PRAGMA table_info(Products)')]:
                db.execute('ALTER TABLE Products DROP COLUMN ImagePath')
            db.execute('DROP TABLE IF EXISTS WalletTopUps')
    with socket.socket() as port_socket:
        port_socket.bind(('127.0.0.1', 0))
        port = port_socket.getsockname()[1]
    base = f'http://127.0.0.1:{port}'
    env = dict(os.environ, SPORTSTORE_DATA_DIR=temp, ASPNETCORE_ENVIRONMENT='Development',
               Logging__LogLevel__Default='Warning')
    log = open(data_dir / 'app.log', 'w+')
    process = subprocess.Popen([args.dotnet, str(ROOT / 'SportStore/bin/Debug/net8.0/SportStore.dll'), '--urls', base],
                               cwd=ROOT / 'SportStore', env=env, stdout=log, stderr=subprocess.STDOUT)
    try:
        for _ in range(100):
            try:
                if urllib.request.urlopen(base, timeout=1).status == 200:
                    break
            except (OSError, urllib.error.URLError):
                if process.poll() is not None:
                    log.seek(0)
                    raise RuntimeError(log.read())
                time.sleep(0.2)
        else:
            raise TimeoutError('Application did not start')

        def scalar(sql, params=()):
            with sqlite3.connect(db_path) as db:
                return db.execute(sql, params).fetchone()[0]

        if baseline:
            check(all(scalar(f'SELECT COUNT(*) FROM {table}') == count for table, count in baseline.items()),
                  'legacy database upgraded without losing users, products or purchases')
        check(scalar("SELECT COUNT(*) FROM pragma_table_info('Products') WHERE name='ImagePath'") == 1,
              'photo column exists after startup')
        check(scalar("SELECT COUNT(*) FROM sqlite_master WHERE name='WalletTopUps'") == 1,
              'top-up table exists after startup')
        if original_demo_ids:
            check(all(scalar('SELECT Id FROM AspNetUsers WHERE Email=?', (email,)) == user_id
                      for email, user_id in original_demo_ids.items()),
                  'short logins upgrade existing demo accounts without replacing their IDs')
        for login, password, destination in [
            ('admin', 'Admin123!', '/Admin'), ('buyer', 'Demo123!', '/Wallet'),
            ('trail', 'Demo123!', '/Seller/Products'), ('arena', 'Demo123!', '/Seller/Products')
        ]:
            demo = Client(base)
            demo.login(login, password)
            check(demo.request(destination)[0] == 200, f'short login works for {login}')
            demo.login(login + '@sport.local', password)
            check(demo.request(destination)[0] == 200, f'previous email login still works for {login}')
            check(scalar('SELECT UserName FROM AspNetUsers WHERE Email=?', (login + '@sport.local',)) == login,
                  f'{login} is stored as the account username')
        invalid_login = Client(base)
        token, login_page = invalid_login.form('/Account/Login')
        check('name="Login"' in login_page and 'autocomplete="username"' in login_page and 'type="email"' not in login_page,
              'login page accepts a username instead of requiring email format')
        status, path, _ = invalid_login.request('/Account/Login', {
            '__RequestVerificationToken': token, 'Login': 'admin', 'Password': 'Incorrect123!'})
        check(status == 200 and path == '/Account/Login' and invalid_login.request('/Admin')[1] == '/Account/Login',
              'short login still requires the correct password')
        buyer = Client(base)
        buyer.login('  BUYER  ', 'Demo123!')
        check(buyer.request('/Seller/CreateProduct')[1] == '/Account/Denied', 'buyer cannot manage seller products')
        buyer_id = scalar("SELECT Id FROM AspNetUsers WHERE Email='buyer@sport.local'")
        balance = lambda: decimal.Decimal(str(scalar('SELECT Balance FROM VirtualCards WHERE UserId=?', (buyer_id,))))
        before = balance()
        token, page = buyer.form('/Wallet/TopUp')
        operation = re.search(r'id="OperationId"[^>]*value="([^"]+)"', page).group(1)
        fields = {'__RequestVerificationToken': token, 'OperationId': operation, 'Amount': '1250.50',
                  'CardNumber': '4242424242424242', 'Expiry': '12/99', 'Cvc': '123'}
        for changes in [{'Amount': '-1'}, {'Amount': '0'}, {'Amount': '1000001'}, {'Amount': '100.001'},
                        {'Expiry': '01/20'}, {'Cvc': ''}, {'Cvc': 'abc'}, {'CardNumber': '123'}]:
            status, path, _ = buyer.request('/Wallet/TopUp', fields | changes)
            check(status == 200 and path == '/Wallet/TopUp' and balance() == before,
                  'invalid top-up rejected: ' + str(changes))
        check(buyer.request('/Wallet/TopUp', fields | {'__RequestVerificationToken': 'invalid'})[0] == 400,
              'top-up requires antiforgery token')
        status, receipt_path, receipt = buyer.request('/Wallet/TopUp', fields)
        check(status == 200 and receipt_path.startswith('/Wallet/Receipt/') and balance() == before + decimal.Decimal('1250.50'),
              'top-up credits exact amount and opens receipt')
        buyer.request('/Wallet/TopUp', fields)
        check(balance() == before + decimal.Decimal('1250.50') and scalar('SELECT COUNT(*) FROM WalletTopUps') == 1,
              'repeated form does not credit twice')
        # Send the same new operation concurrently: serializable transaction + primary key protects the balance.
        concurrent_fields = fields | {'OperationId': str(uuid.uuid4()), 'Amount': '200'}
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            list(pool.map(lambda _: buyer.request('/Wallet/TopUp', concurrent_fields), range(2)))
        check(balance() == before + decimal.Decimal('1450.50') and scalar('SELECT COUNT(*) FROM WalletTopUps') == 2,
              'concurrent repeated top-up credits once')
        with sqlite3.connect(db_path) as db:
            columns = [row[1] for row in db.execute('PRAGMA table_info(WalletTopUps)')]
        check(not any(name in columns for name in ['CardNumber', 'Expiry', 'Cvc']), 'card credentials are not stored')
        check('Пополнение карты' in html.unescape(buyer.request('/Wallet')[2].decode()), 'wallet shows top-up history')

        seller = Client(base)
        seller.login('trail', 'Demo123!')
        check(seller.request('/Wallet/TopUp')[1] == '/Account/Denied', 'seller cannot top up buyer cards')
        token, _ = seller.form('/Seller/CreateProduct')
        product_fields = {'__RequestVerificationToken': token, 'Name': 'Integration photo product',
                          'Description': 'Product used only in a temporary test database.', 'Price': '1500.25',
                          'Stock': '8', 'CategoryId': '1', 'Visual': 'default', 'IsPublished': 'true'}
        product_count = scalar('SELECT COUNT(*) FROM Products')
        status, path, _ = seller.request('/Seller/CreateProduct', product_fields, ('fake.jpg', b'not an image', 'image/jpeg'))
        check(status == 200 and path == '/Seller/CreateProduct' and scalar('SELECT COUNT(*) FROM Products') == product_count,
              'renamed non-image upload rejected on server')
        png = base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=')
        status, path, _ = seller.request('/Seller/CreateProduct', product_fields, ('large.png', png + b'0' * (5 * 1024 * 1024), 'image/png'))
        check(status == 200 and path == '/Seller/CreateProduct' and scalar('SELECT COUNT(*) FROM Products') == product_count,
              'photo exceeding 5 MB rejected on server')
        status, path, _ = seller.request('/Seller/CreateProduct', product_fields, ('photo.png', png, 'image/png'))
        product_id = scalar("SELECT Id FROM Products WHERE Name='Integration photo product'")
        image_path = scalar('SELECT ImagePath FROM Products WHERE Id=?', (product_id,))
        check(status == 200 and path == '/Seller/Products' and buyer.request(image_path)[2] == png,
              'valid photo saved and served unchanged')
        check(image_path in buyer.request('/Catalog')[2].decode() and image_path in buyer.request(f'/Catalog/Details/{product_id}')[2].decode(),
              'photo rendered in catalogue and product details')
        edit_path = f'/Seller/EditProduct/{product_id}'
        token, _ = seller.form(edit_path)
        product_fields['__RequestVerificationToken'] = token
        seller.request(edit_path, product_fields)
        check(scalar('SELECT ImagePath FROM Products WHERE Id=?', (product_id,)) == image_path, 'editing without upload preserves photo')
        seller.request(edit_path, product_fields, ('replacement.png', png, 'image/png'))
        replacement_path = scalar('SELECT ImagePath FROM Products WHERE Id=?', (product_id,))
        check(replacement_path != image_path and buyer.request(image_path)[0] == 404, 'replacing photo cleans up old file')
        token, _ = buyer.form(f'/Catalog/Details/{product_id}')
        buyer.request('/Cart/Add', {'__RequestVerificationToken': token, 'productId': product_id, 'quantity': '1'})
        check(replacement_path in buyer.request('/Cart')[2].decode(), 'photo rendered in cart')
        token, _ = buyer.form('/Checkout')
        checkout = {'__RequestVerificationToken': token, 'RecipientName': 'Тестовый покупатель',
                    'DeliveryAddress': 'Тестовый адрес', 'ContactPhone': '+7 (900) 123-45-67'}
        purchases_before = scalar('SELECT COUNT(*) FROM Purchases')
        payment_balance = balance()
        for phone in ['abc', '79001234567', '+79001234567', '+7 (900) 123-45', '+1 (900) 123-45-67', '123', '1' * 16]:
            status, path, _ = buyer.request('/Checkout', checkout | {'ContactPhone': phone})
            check(status == 200 and path == '/Checkout' and scalar('SELECT COUNT(*) FROM Purchases') == purchases_before and balance() == payment_balance,
                  'server rejects invalid phone: ' + phone)
        # Clear legacy cart entries, so the assertion concerns only our temporary product.
        with sqlite3.connect(db_path) as db:
            db.execute('DELETE FROM CartItems WHERE BuyerId=? AND ProductId<>?', (buyer_id, product_id))
        status, path, _ = buyer.request('/Checkout', checkout)
        check(status == 200 and path.startswith('/Orders/Details/') and balance() == payment_balance - decimal.Decimal('1500.25'),
              'complete formatted phone completes purchase using topped-up balance')
        check('Покупка №' in html.unescape(buyer.request('/Wallet')[2].decode()), 'wallet shows purchase and top-up history together')
        seller.request(edit_path, product_fields | {'RemovePhoto': 'true'})
        check(scalar('SELECT ImagePath FROM Products WHERE Id=?', (product_id,)) is None and buyer.request(replacement_path)[0] == 404,
              'removing photo deletes file and restores placeholder')
        admin = Client(base)
        admin.login('admin', 'Admin123!')
        check(admin.request('/Admin')[0] == 200, 'documented administrator credentials work')
        token, _ = admin.form('/Admin/Products')
        admin.request(f'/Admin/HideProduct/{product_id}', {'__RequestVerificationToken': token, 'reason': 'Исправить описание'})
        check(buyer.request(f'/Catalog/Details/{product_id}')[0] == 404, 'moderated product stays hidden')
        token, _ = seller.form(edit_path)
        seller.request(edit_path, product_fields | {'__RequestVerificationToken': token}, ('fixed.png', png, 'image/png'))
        check(scalar('SELECT ModerationStatus FROM Products WHERE Id=?', (product_id,)) == 'PendingReview'
              and buyer.request(f'/Catalog/Details/{product_id}')[0] == 404,
              'editing photo preserves moderation review requirement')
        token, _ = admin.form('/Admin/Products')
        admin.request(f'/Admin/ApproveProduct/{product_id}', {'__RequestVerificationToken': token})
        moderated_image = scalar('SELECT ImagePath FROM Products WHERE Id=?', (product_id,))
        check(buyer.request(f'/Catalog/Details/{product_id}')[0] == 200 and buyer.request(moderated_image)[2] == png,
              'administrator approval restores product with uploaded photo')
        other_buyer = Client(base)
        token, _ = other_buyer.form('/Account/Register')
        status, path, _ = other_buyer.request('/Account/Register', {
            '__RequestVerificationToken': token, 'DisplayName': 'Temporary buyer', 'Email': 'temporary@example.local',
            'Password': 'TestPass123!', 'ConfirmPassword': 'TestPass123!', 'Role': 'Buyer'})
        assert status == 200 and path == '/'
        other_buyer.login('temporary@example.local', 'TestPass123!')
        check(other_buyer.request(receipt_path)[0] == 404, 'receipt visible only to its owner')
        print('All integration checks passed. Test data was isolated.', flush=True)
    finally:
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
        log.close()
