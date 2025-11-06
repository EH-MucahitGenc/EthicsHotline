// wwwroot/js/otp-modal.js
import { postJson } from './api.js';
import { $, isValidTrMobile, e164FromInput } from './validators.js';
import { toast } from './toast.js';

let cooldown = 30;
let cooldownTimer = null;

// Dışarıdan çağırılacak tek entry
// opts: { onSubmit(payload), buildPayload(), requireOtpGetter() }
export function initOtpModal(opts) {
    const el = {
        modal: document.getElementById('otpModal'),
        phoneRow: document.getElementById('mPhoneRow'),
        codeRow: document.getElementById('mCodeRow'),
        phone: document.getElementById('mPhone'),
        phoneErr: document.getElementById('mPhoneErr'),
        code: document.getElementById('mCode'),
        err: document.getElementById('mErr'),
        sendBtn: document.getElementById('mSendBtn'),
        verifyBtn: document.getElementById('mVerifyBtn'),
        resendBtn: document.getElementById('mResendBtn'),
        changeBtn: document.getElementById('mChangePhone'),
        confirmBtn: document.getElementById('mConfirmSend')
    };

    function reset() {
        el.phone.value = '';
        el.code.value = '';
        el.phoneErr.classList.add('d-none');
        el.err.classList.add('d-none');

        el.sendBtn.disabled = false;
        el.confirmBtn.disabled = true;

        el.phoneRow.classList.remove('d-none');
        el.codeRow.classList.add('d-none');

        el.resendBtn.disabled = true;
        el.resendBtn.textContent = 'Tekrar Gönder (30)';
        if (cooldownTimer) { clearInterval(cooldownTimer); cooldownTimer = null; }
    }

    function startCooldown() {
        el.resendBtn.disabled = true;
        let left = cooldown;
        el.resendBtn.textContent = `Tekrar Gönder (${left})`;
        cooldownTimer = setInterval(() => {
            left--;
            el.resendBtn.textContent = `Tekrar Gönder (${left})`;
            if (left <= 0) {
                clearInterval(cooldownTimer);
                cooldownTimer = null;
                el.resendBtn.disabled = false;
                el.resendBtn.textContent = 'Tekrar Gönder';
            }
        }, 1000);
    }

    // Kodu Gönder
    el.sendBtn.addEventListener('click', async () => {
        el.err.classList.add('d-none');
        const d = el.phone.value.trim();
        if (!isValidTrMobile(d)) {
            el.phoneErr.classList.remove('d-none');
            return;
        }
        el.phoneErr.classList.add('d-none');

        try {
            el.sendBtn.disabled = true;
            await postJson('/otp/send', { phone: `+90${d}` });
            el.phoneRow.classList.add('d-none');
            el.codeRow.classList.remove('d-none');
            startCooldown();
            toast('Kod gönderildi', 'primary');
            setTimeout(() => el.code.focus(), 60);
        } catch (e) {
            el.err.textContent = e.message || 'SMS gönderilemedi';
            el.err.classList.remove('d-none');
            el.sendBtn.disabled = false;
        }
    });

    // Tekrar Gönder
    el.resendBtn.addEventListener('click', async () => {
        try {
            await postJson('/otp/send', { phone: e164FromInput('#mPhone') });
            startCooldown();
            toast('Kod tekrar gönderildi', 'primary');
        } catch (e) {
            el.err.textContent = e.message || 'Tekrar gönderilemedi';
            el.err.classList.remove('d-none');
        }
    });

    // Telefonu değiştir
    el.changeBtn.addEventListener('click', () => {
        if (cooldownTimer) { clearInterval(cooldownTimer); cooldownTimer = null; }
        el.code.value = '';
        el.codeRow.classList.add('d-none');
        el.phoneRow.classList.remove('d-none');
        el.sendBtn.disabled = false;
    });

    // Doğrula -> Bildirimi Gönder enable
    el.verifyBtn.addEventListener('click', async () => {
        el.err.classList.add('d-none');
        const phone = e164FromInput('#mPhone');
        const code = el.code.value.trim();

        if (!isValidTrMobile(el.phone.value.trim())) {
            el.phoneErr.classList.remove('d-none');
            return;
        }
        if (!code) {
            el.err.textContent = 'Kod gerekli';
            el.err.classList.remove('d-none');
            return;
        }

        try {
            const r = await postJson('/otp/verify', { phone, code });
            if (r && r.success) {
                el.confirmBtn.disabled = false;
                toast('Doğrulama başarılı. Bildirimi Gönder ile tamamlayın.', 'success');
            }
        } catch (e) {
            el.err.textContent = e.message || 'Doğrulama başarısız';
            el.err.classList.remove('d-none');
        }
    });

    // Bildirimi Gönder (modal içinden)
    el.confirmBtn.addEventListener('click', async () => {
        // requireOtp true ise ve code alanı boşsa göndermeyelim (ek koruma)
        if (opts.requireOtpGetter?.() && !el.code.value.trim()) {
            el.err.textContent = 'Kod gerekli';
            el.err.classList.remove('d-none');
            return;
        }

        await opts.onSubmit?.(true); // closeModal=true
    });

    // Dışarıya modal açarken çağıracağın reset fonksiyonu
    return {
        open() {
            reset();
            new bootstrap.Modal(el.modal).show();
        }
    };
}
