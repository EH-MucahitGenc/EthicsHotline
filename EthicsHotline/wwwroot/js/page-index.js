// wwwroot/js/page-index.js
import { getJson, postJson } from './api.js';
import { toast } from './toast.js';
import { $, validateRequiredFields } from './validators.js';
import { initOtpModal } from './otp-modal.js';

let REQUIRE_OTP = true;
let isSubmitting = false;

function buildPayload() {
    const catSel = $('#category');
    const categoryText = catSel?.selectedOptions?.length ? catSel.selectedOptions[0].text : '';

    return {
        category: categoryText,
        eventDate: $('#eventDate').value,
        eventTime: $('#eventTime').value ? $('#eventTime').value + ':00' : '00:00:00',
        location: $('#location').value,
        details: $('#details').value,
        people: $('#people').value,
        // OTP modal içindeki telefon+kodu api tarafında zorunlu kontrol ediliyor.
        phone: document.querySelector('#mPhone')?.value?.trim()?.length === 10 ? `+90${document.querySelector('#mPhone').value.trim()}` : '',
        otpCode: document.querySelector('#mCode')?.value?.trim() || '',
        kvkkConsent: $('#kvkk').checked
    };
}

async function doSubmit(closeModal) {
    if (isSubmitting) return;
    isSubmitting = true;
    try {
        const p = buildPayload();

        // Ek güvenlik (sunucu tarafında da var)
        if (!p.kvkkConsent) {
            toast('KVKK aydınlatmasını onaylayın.', 'warning');
            isSubmitting = false; return;
        }
        if (!p.category) {
            toast('Kategori seçin.', 'warning');
            isSubmitting = false; return;
        }
        if (!p.details?.trim()) {
            toast('Detay alanı zorunludur.', 'warning');
            isSubmitting = false; return;
        }

        const res = await postJson('/form/submit', p);
        if (closeModal) {
            const modalEl = document.getElementById('otpModal');
            bootstrap.Modal.getInstance(modalEl)?.hide();
        }
        document.getElementById('successMsg').textContent = res.message || 'Bildiriminiz iletilmiştir.';
        new bootstrap.Modal(document.getElementById('successModal')).show();
    } catch (e) {
        toast(e.message || 'Gönderim başarısız', 'danger');
    } finally {
        isSubmitting = false;
    }
}

export async function initIndexPage() {
    // Features
    try {
        const f = await getJson('/features');
        REQUIRE_OTP = !!f.requireOtp;
    } catch { /* varsayılan true kalsın */ }

    // OTP Modal’ı başlat
    const otp = initOtpModal({
        onSubmit: doSubmit,
        buildPayload,
        requireOtpGetter: () => REQUIRE_OTP
    });

    // KVKK hızlı onay (modal)
    document.getElementById('kvkkApprove')?.addEventListener('click', () => {
        const kvkk = document.getElementById('kvkk');
        if (kvkk) kvkk.checked = true;
        bootstrap.Modal.getInstance(document.getElementById('kvkkModal'))?.hide();
        toast('KVKK onayı verildi', 'success');
    });

    // Başarı modalı kapatıldığında sayfayı yenile
    document.getElementById('closeSuccessBtn')?.addEventListener('click', () => window.location.reload());
    document.getElementById('successModal')?.addEventListener('hidden.bs.modal', () => window.location.reload());
    document.getElementById('newReportBtn')?.addEventListener('click', () => {
        bootstrap.Modal.getInstance(document.getElementById('successModal'))?.hide();
    });

    // Ana “Gönder” butonu
    document.getElementById('submitBtn')?.addEventListener('click', (ev) => {
        ev.preventDefault();

        // Zorunlu alanlar
        if (!validateRequiredFields()) {
            // Hata görsellerini Bootstrap ile göstermek için:
            // #category ve #details alanlarına .is-invalid class zaten basılıyor
            return;
        }

        if (!document.getElementById('kvkk').checked) {
            toast('KVKK aydınlatmasını onaylayın.', 'warning');
            return;
        }

        if (!REQUIRE_OTP) {
            doSubmit(false);
            return;
        }

        // OTP açık ise modal akışı
        otp.open();
    });
}
