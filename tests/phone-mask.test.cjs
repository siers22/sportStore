const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { test } = require('node:test');
const vm = require('node:vm');

function setup(initial = '') {
    const input = new EventTarget();
    Object.assign(input, {
        value: initial, selectionStart: initial.length, selectionEnd: initial.length,
        setSelectionRange(start, end) { this.selectionStart = start; this.selectionEnd = end; }
    });
    const document = { activeElement: null, querySelectorAll: () => [input] };
    vm.runInNewContext(readFileSync(join(__dirname, '../SportStore/wwwroot/js/phone-mask.js'), 'utf8'), { document, Event });
    document.activeElement = input;
    input.dispatchEvent(new Event('focus'));
    return { input, document };
}

function insert(input, text) {
    const start = input.selectionStart;
    input.value = input.value.slice(0, start) + text + input.value.slice(input.selectionEnd);
    input.setSelectionRange(start + text.length, start + text.length);
    input.dispatchEvent(new Event('input'));
}

function paste(input, text) {
    const event = new Event('paste', { cancelable: true });
    event.clipboardData = { getData: () => text };
    if (input.dispatchEvent(event)) insert(input, text);
}

function erase(input, direction = 'deleteContentBackward') {
    const event = new Event('beforeinput', { cancelable: true });
    event.inputType = direction;
    input.dispatchEvent(event);
    assert.equal(event.defaultPrevented, true);
}

test('number assembles during typing, without typing punctuation', () => {
    const { input } = setup();
    assert.equal(input.value, '+7');
    for (const [digits, formatted] of [
        ['9', '+7 (9'], ['00', '+7 (900)'], ['123', '+7 (900) 123'],
        ['45', '+7 (900) 123-45'], ['67', '+7 (900) 123-45-67']
    ]) {
        for (const digit of digits) insert(input, digit);
        assert.equal(input.value, formatted);
        assert.equal(input.selectionStart, formatted.length);
    }
    insert(input, '8');
    assert.equal(input.value, '+7 (900) 123-45-67');
});

test('full numbers pasted with +7, 7, 8 or without country code normalize equally', () => {
    for (const number of ['+7 (900) 123-45-67', '79001234567', '89001234567', '9001234567']) {
        const { input } = setup();
        paste(input, number);
        assert.equal(input.value, '+7 (900) 123-45-67', number);
    }
});

test('typing domestic number with leading 8 also normalizes', () => {
    const { input } = setup();
    for (const digit of '89001234567') insert(input, digit);
    assert.equal(input.value, '+7 (900) 123-45-67');
});

test('backspace crosses separators and can erase the whole number', () => {
    const { input } = setup('+7 (900) 123-45-67');
    input.setSelectionRange(16, 16); // After the last hyphen.
    erase(input);
    assert.equal(input.value, '+7 (900) 123-46-7');
    input.setSelectionRange(input.value.length, input.value.length);
    for (let index = 0; index < 10; index++) erase(input);
    assert.equal(input.value, '+7');
    erase(input);
    assert.equal(input.value, '+7');
});

test('forward delete removes the next digit across punctuation', () => {
    const { input } = setup('+7 (900) 123-45-67');
    input.setSelectionRange(7, 7); // Before the closing parenthesis.
    erase(input, 'deleteContentForward');
    assert.equal(input.value, '+7 (900) 234-56-7');
});

test('selection replacement preserves the remaining digits and caret', () => {
    const { input } = setup('+7 (900) 123-45-67');
    input.setSelectionRange(4, 7);
    insert(input, '912');
    assert.equal(input.value, '+7 (912) 123-45-67');
    assert.equal(input.selectionStart, 9);
    insert(input, 'x');
    assert.equal(input.value, '+7 (912) 123-45-67');
    assert.equal(input.selectionStart, 9);
});

test('selection deletion and blur clear an empty field for required validation', () => {
    const { input, document } = setup('+7 (900) 123-45-67');
    input.setSelectionRange(0, input.value.length);
    erase(input);
    assert.equal(input.value, '+7');
    document.activeElement = null;
    input.dispatchEvent(new Event('blur'));
    assert.equal(input.value, '');
});

test('browser-restored unformatted numbers are formatted on initialization', () => {
    assert.equal(setup('89001234567').input.value, '+7 (900) 123-45-67');
});
