// Lightweight address autocomplete using Photon (https://photon.komoot.io) — free, no API key.
// Attach by adding data-address-autocomplete to any <input>.
(function () {
    const minChars = 3;
    const debounceMs = 250;

    function debounce(fn, ms) {
        let t;
        return function (...args) {
            clearTimeout(t);
            t = setTimeout(() => fn.apply(this, args), ms);
        };
    }

    function buildLabel(props) {
        const parts = [props.name, props.street, props.housenumber, props.city, props.state, props.country]
            .filter(Boolean);
        // Deduplicate name if it equals street
        const seen = new Set();
        return parts.filter(p => {
            if (seen.has(p)) return false;
            seen.add(p);
            return true;
        }).join(', ');
    }

    function attach(input) {
        const wrapper = document.createElement('div');
        wrapper.style.position = 'relative';
        input.parentNode.insertBefore(wrapper, input);
        wrapper.appendChild(input);

        const list = document.createElement('div');
        list.className = 'list-group position-absolute w-100 shadow-sm';
        list.style.zIndex = '1050';
        list.style.maxHeight = '260px';
        list.style.overflowY = 'auto';
        list.style.display = 'none';
        wrapper.appendChild(list);

        let lastResults = [];
        let activeIdx = -1;

        function render() {
            list.innerHTML = '';
            if (!lastResults.length) {
                list.style.display = 'none';
                return;
            }
            lastResults.forEach((r, i) => {
                const item = document.createElement('button');
                item.type = 'button';
                item.className = 'list-group-item list-group-item-action' + (i === activeIdx ? ' active' : '');
                item.textContent = r;
                item.addEventListener('mousedown', (e) => {
                    e.preventDefault();
                    input.value = r;
                    lastResults = [];
                    render();
                });
                list.appendChild(item);
            });
            list.style.display = 'block';
        }

        const search = debounce(async (q) => {
            if (q.length < minChars) {
                lastResults = [];
                render();
                return;
            }
            try {
                const url = `https://photon.komoot.io/api/?q=${encodeURIComponent(q)}&limit=6`;
                const res = await fetch(url);
                if (!res.ok) return;
                const data = await res.json();
                lastResults = (data.features || [])
                    .map(f => buildLabel(f.properties || {}))
                    .filter(Boolean);
                activeIdx = -1;
                render();
            } catch (e) {
                // ignore network errors silently
            }
        }, debounceMs);

        input.setAttribute('autocomplete', 'off');
        input.addEventListener('input', () => search(input.value.trim()));
        input.addEventListener('keydown', (e) => {
            if (list.style.display === 'none') return;
            if (e.key === 'ArrowDown') { e.preventDefault(); activeIdx = Math.min(activeIdx + 1, lastResults.length - 1); render(); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); activeIdx = Math.max(activeIdx - 1, 0); render(); }
            else if (e.key === 'Enter' && activeIdx >= 0) {
                e.preventDefault();
                input.value = lastResults[activeIdx];
                lastResults = [];
                render();
            } else if (e.key === 'Escape') {
                lastResults = [];
                render();
            }
        });
        input.addEventListener('blur', () => {
            // Delay so click handlers fire first
            setTimeout(() => { lastResults = []; render(); }, 150);
        });
    }

    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('input[data-address-autocomplete]').forEach(attach);
    });
})();
