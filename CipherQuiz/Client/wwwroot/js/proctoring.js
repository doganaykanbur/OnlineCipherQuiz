window.proctoring = {
    dotNetRef: null,

    start: function (ref) {
        this.dotNetRef = ref;
        document.addEventListener('copy', this.handleEvent);
        document.addEventListener('paste', this.handleEvent);
        document.addEventListener('contextmenu', this.handleEvent);
        document.addEventListener('keydown', this.handleKey);
        document.addEventListener('visibilitychange', this.handleVisibility);
        window.addEventListener('blur', this.handleBlur);
        window.addEventListener('focus', this.handleFocus);
    },

    stop: function () {
        document.removeEventListener('copy', this.handleEvent);
        document.removeEventListener('paste', this.handleEvent);
        document.removeEventListener('contextmenu', this.handleEvent);
        document.removeEventListener('visibilitychange', this.handleVisibility);
        window.removeEventListener('blur', this.handleBlur);
        window.removeEventListener('focus', this.handleFocus);
        document.removeEventListener('keydown', this.handleKey);
        this.dotNetRef = null;
        this.hideToast();
    },

    handleEvent: function (e) {
        if (e.type === 'copy' || e.type === 'paste' || e.type === 'contextmenu') {
            e.preventDefault();
            window.proctoring.showToast('Kopya engellendi');
        }
        if (window.proctoring.dotNetRef) {
            window.proctoring.dotNetRef.invokeMethodAsync('OnProctorEvent', {
                type: e.type,
                content: '',
                timestampUtc: new Date().toISOString()
            });
        }
    },

    handleVisibility: function () {
        if (window.proctoring.dotNetRef) {
            window.proctoring.dotNetRef.invokeMethodAsync('OnProctorEvent', {
                type: 'visibility',
                content: document.hidden ? 'hidden' : 'visible',
                timestampUtc: new Date().toISOString()
            });
        }
    },

    handleBlur: function () {
        if (window.proctoring.dotNetRef) {
            window.proctoring.dotNetRef.invokeMethodAsync('OnProctorEvent', {
                type: 'blur',
                content: 'window_blur',
                timestampUtc: new Date().toISOString()
            });
        }
    },

    handleFocus: function () {
        if (window.proctoring.dotNetRef) {
            window.proctoring.dotNetRef.invokeMethodAsync('OnProctorEvent', {
                type: 'focus',
                content: 'window_focus',
                timestampUtc: new Date().toISOString()
            });
        }
    },

    handleKey: function (e) {
        const isCopy = e.ctrlKey && (e.key === 'c' || e.key === 'C');
        const isPaste = e.ctrlKey && (e.key === 'v' || e.key === 'V');
        if (isCopy || isPaste) {
            e.preventDefault();
            window.proctoring.showToast('Kopya engellendi');
            if (window.proctoring.dotNetRef) {
                window.proctoring.dotNetRef.invokeMethodAsync('OnProctorEvent', {
                    type: isCopy ? 'copy' : 'paste',
                    content: 'blocked',
                    timestampUtc: new Date().toISOString()
                });
            }
        }
    },

    showToast: function (text) {
        if (!this.toastEl) {
            const el = document.createElement('div');
            el.id = 'proctor-toast';
            el.style.position = 'fixed';
            el.style.bottom = '16px';
            el.style.right = '16px';
            el.style.background = 'rgba(220, 53, 69, 0.9)';
            el.style.color = '#fff';
            el.style.padding = '10px 14px';
            el.style.borderRadius = '8px';
            el.style.fontWeight = '700';
            el.style.fontFamily = 'sans-serif';
            el.style.boxShadow = '0 8px 20px rgba(0,0,0,0.3)';
            el.style.zIndex = '2000';
            document.body.appendChild(el);
            this.toastEl = el;
        }
        this.toastEl.textContent = `⚠️ ${text}`;
        this.toastEl.style.display = 'block';
        if (navigator.vibrate) { navigator.vibrate(100); }
        clearTimeout(this.toastTimer);
        this.toastTimer = setTimeout(() => this.hideToast(), 2000);
    },

    hideToast: function () {
        if (this.toastEl) {
            this.toastEl.style.display = 'none';
        }
    }
};

window.startProctoring = (ref) => window.proctoring.start(ref);
window.stopProctoring = () => window.proctoring.stop();
