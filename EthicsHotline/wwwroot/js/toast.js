// wwwroot/js/toast.js
export function toast(message, variant = 'primary', delay = 2500) {
    const wrap = document.getElementById('toastWrap') || (() => {
        const d = document.createElement('div');
        d.id = 'toastWrap';
        d.className = 'toast-container position-fixed top-0 end-0 p-3';
        d.style.zIndex = '1080';
        document.body.appendChild(d);
        return d;
    })();

    const div = document.createElement('div');
    div.className = `toast align-items-center text-bg-${variant} border-0`;
    div.innerHTML = `
    <div class="d-flex">
      <div class="toast-body">${message}</div>
      <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
    </div>`;
    wrap.appendChild(div);

    const t = new bootstrap.Toast(div, { delay });
    t.show();
    div.addEventListener('hidden.bs.toast', () => div.remove());
}
