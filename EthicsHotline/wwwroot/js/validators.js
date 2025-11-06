// wwwroot/js/validators.js
export const $ = (s) => document.querySelector(s);

export function isValidTrMobile(v) {
    return /^5\d{9}$/.test((v || '').trim());
}

export function e164FromInput(inputSelector) {
    const d = $(inputSelector).value.trim();
    return d.length === 10 ? `+90${d}` : '';
}

// Form zorunlu alan validasyonu (Kategori + Detay)
export function validateRequiredFields() {
    const category = $('#category');
    const details = $('#details');

    category.classList.remove('is-invalid');
    details.classList.remove('is-invalid');

    const catOk = !!category.value;
    const detOk = !!(details.value || '').trim();

    if (!catOk) category.classList.add('is-invalid');
    if (!detOk) details.classList.add('is-invalid');

    return (catOk && detOk);
}
