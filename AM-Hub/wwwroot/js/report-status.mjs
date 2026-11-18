class ReportStatus extends HTMLElement {
    connectedCallback() {
        this.form = this.querySelector('form');
        this.submit ??= event => this.save(event);
        this.form?.addEventListener('submit', this.submit);
    }

    disconnectedCallback() {
        this.form?.removeEventListener('submit', this.submit);
    }

    async save(event) {
        event.preventDefault();
        if (this.busy || this.checked) return;
        this.busy = true;
        const button = event.submitter ?? this.form.querySelector('[data-status="Checked"]');
        const target = button.dataset.status;
        const buttons = this.form.querySelectorAll('button');
        const feedback = this.form.querySelector('.status-feedback');
        const label = button.textContent;
        buttons.forEach(item => item.disabled = true);
        button.textContent = 'Status wijzigen…';
        this.form.setAttribute('aria-busy', 'true');
        feedback.hidden = false;
        feedback.textContent = 'De status wordt gewijzigd…';
        feedback.setAttribute('role', 'status');
        try {
            const response = await fetch(button.hasAttribute('formaction') ? button.formAction : this.form.action, {
                method: 'POST',
                body: new FormData(this.form),
                headers: { Accept: 'application/json' },
                credentials: 'same-origin',
                redirect: 'error'
            });
            if (!response.ok) {
                const contentType = response.headers.get('content-type') ?? '';
                const message = contentType.startsWith('text/plain') ? await response.text() : '';
                throw new Error(message || `De status kon niet worden gewijzigd (HTTP ${response.status}). Vernieuw de pagina en controleer je aanmelding.`);
            }
            const result = await response.json();
            if (result.status !== target) throw new Error('Onverwacht antwoord. Controleer de status in BC voordat je opnieuw probeert.');
            this.checked = true;
            const status = this.closest('.workorder-information')?.querySelector('[data-workorder-status]');
            if (status) status.textContent = result.status;
            feedback.textContent = result.message;
            button.textContent = `Status: ${result.status}`;
        } catch (error) {
            feedback.setAttribute('role', 'alert');
            feedback.textContent = error instanceof TypeError
                ? 'De verbinding is onderbroken of je aanmelding is verlopen. Controleer de status in BC en vernieuw de pagina voordat je opnieuw probeert.'
                : error.message;
        } finally {
            this.busy = false;
            this.form.removeAttribute('aria-busy');
            buttons.forEach(item => item.disabled = Boolean(this.checked));
            if (!this.checked) button.textContent = label;
        }
    }
}

customElements.define('hub-report-status', ReportStatus);
