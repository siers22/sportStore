document.querySelectorAll('[data-digits-only]').forEach(input => {
    input.addEventListener('input', () => { input.value = input.value.replace(/[^0-9]/g, ''); });
});

const photoInput = document.getElementById('Photo');
const photoPreview = document.getElementById('photo-preview');
if (photoInput && photoPreview) {
    const existingPhoto = photoPreview.getAttribute('src');
    let previewUrl;
    photoInput.addEventListener('change', () => {
        if (previewUrl) URL.revokeObjectURL(previewUrl);
        const photo = photoInput.files[0];
        photoInput.setCustomValidity('');
        if (photo && (photo.size > 5 * 1024 * 1024 || !['image/jpeg', 'image/png', 'image/webp'].includes(photo.type))) {
            photoInput.setCustomValidity('Выберите изображение JPG, PNG или WebP размером до 5 МБ.');
            photoInput.reportValidity();
            photoPreview.hidden = true;
            return;
        }
        previewUrl = photo ? URL.createObjectURL(photo) : null;
        photoPreview.src = previewUrl || existingPhoto || '';
        photoPreview.hidden = !previewUrl && !existingPhoto;
    });
}

const amountInput = document.getElementById('Amount');
if (amountInput) {
    const renderAmount = () => {
        const value = Number(amountInput.value);
        const text = new Intl.NumberFormat('ru-RU', { style: 'currency', currency: 'RUB' }).format(Number.isFinite(value) ? value : 0);
        document.getElementById('topup-button-amount').textContent = text;
        document.getElementById('topup-summary-amount').textContent = text;
    };
    amountInput.addEventListener('input', renderAmount);
    document.querySelectorAll('[data-amount]').forEach(button => {
        button.addEventListener('click', () => { amountInput.value = button.dataset.amount; renderAmount(); });
    });
    renderAmount();
}

const expiryInput = document.getElementById('Expiry');
expiryInput?.addEventListener('input', () => {
    const digits = expiryInput.value.replace(/[^0-9]/g, '').slice(0, 4);
    expiryInput.value = digits.length > 2 ? `${digits.slice(0, 2)}/${digits.slice(2)}` : digits;
});
