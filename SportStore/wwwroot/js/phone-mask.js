document.querySelectorAll('[data-phone-mask]').forEach(input => {
    const digitsOnly = value => value.replace(/[^0-9]/g, '');
    const nationalNumber = value => {
        let digits = digitsOnly(value);
        if (value.trimStart().startsWith('+7')) digits = digits.slice(1);
        // Accept a full number pasted with +7, 7 or the domestic prefix 8.
        if (digits.length === 11 && /^[78]/.test(digits)) digits = digits.slice(1);
        return digits.slice(0, 10);
    };
    const format = digits => {
        let value = '+7';
        if (digits.length) value += ' (' + digits.slice(0, 3);
        if (digits.length >= 3) value += ')';
        if (digits.length > 3) value += ' ' + digits.slice(3, 6);
        if (digits.length > 6) value += '-' + digits.slice(6, 8);
        if (digits.length > 8) value += '-' + digits.slice(8, 10);
        return value;
    };
    const digitPositions = value => [...value].flatMap((character, index) =>
        index > 1 && /[0-9]/.test(character) ? [index] : []);
    const render = (digits, caret) => {
        input.value = digits.length || document.activeElement === input ? format(digits) : '';
        if (caret !== undefined) {
            const positions = digitPositions(input.value);
            const position = positions[caret] ?? input.value.length;
            input.setSelectionRange(position, position);
        }
    };

    input.addEventListener('focus', () => {
        const digits = nationalNumber(input.value);
        render(digits, digits.length);
    });
    input.addEventListener('blur', () => {
        if (!nationalNumber(input.value)) input.value = '';
    });
    input.addEventListener('input', () => {
        const value = input.value;
        const cursor = input.selectionStart ?? value.length;
        const hasPrefix = value.trimStart().startsWith('+7');
        let caret = digitsOnly(value.slice(0, cursor)).length - (hasPrefix ? 1 : 0);
        const digits = nationalNumber(value);
        if (digitsOnly(value).length - (hasPrefix ? 1 : 0) === 11 && /^[78]/.test(digitsOnly(value).slice(hasPrefix ? 1 : 0)))
            caret--;
        render(digits, Math.max(0, Math.min(caret, digits.length)));
    });
    input.addEventListener('beforeinput', event => {
        if (!['deleteContentBackward', 'deleteContentForward'].includes(event.inputType)) return;
        event.preventDefault();
        const start = input.selectionStart ?? input.value.length;
        const end = input.selectionEnd ?? start;
        const positions = digitPositions(input.value);
        let from = positions.filter(position => position < start).length;
        let to = positions.filter(position => position < end).length;
        if (start === end) {
            if (event.inputType === 'deleteContentBackward') from = Math.max(0, from - 1);
            else to = Math.min(positions.length, to + 1);
        }
        const digits = nationalNumber(input.value);
        render(digits.slice(0, from) + digits.slice(to), from);
        input.dispatchEvent(new Event('input', { bubbles: true }));
    });
    input.addEventListener('paste', event => {
        const text = event.clipboardData?.getData('text');
        if (!text) return;
        const digits = digitsOnly(text);
        if (digits.length !== 10 && !(digits.length === 11 && /^[78]/.test(digits))) return;
        event.preventDefault();
        const national = nationalNumber(text);
        render(national, national.length);
        input.dispatchEvent(new Event('input', { bubbles: true }));
    });

    // Also format values restored by browser autofill or a failed form submission.
    if (input.value) render(nationalNumber(input.value));
});
