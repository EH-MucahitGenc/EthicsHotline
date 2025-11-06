// wwwroot/js/api.js
export function getCsrf() {
    return document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') || '';
}

export async function postJson(url, obj) {
    const res = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': getCsrf() },
        body: JSON.stringify(obj)
    });

    const raw = await res.text();
    let data = {};
    if (raw) { try { data = JSON.parse(raw); } catch { /* non-json */ } }

    if (!res.ok) {
        throw new Error((data && data.message) || `HTTP ${res.status}`);
    }
    return data;
}

export async function getJson(url) {
    const res = await fetch(url, { headers: { 'Accept': 'application/json' } });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return res.json();
}
