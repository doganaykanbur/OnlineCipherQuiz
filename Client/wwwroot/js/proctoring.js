window.proctoring = {
    dotNetRef: null,

    start: function (ref) {
        this.dotNetRef = ref;
        document.addEventListener('copy', this.handleEvent);
        document.addEventListener('paste', this.handleEvent);
        document.addEventListener('contextmenu', this.handleEvent);
        document.addEventListener('keydown', this.handleKey);
        document.addEventListener('keyup', this.handleKeyUp);
        document.addEventListener('visibilitychange', this.handleVisibility);
        document.addEventListener('fullscreenchange', this.handleFullscreen);
        window.addEventListener('blur', this.handleBlur);
        window.addEventListener('focus', this.handleFocus);
    },

    stop: function () {
        document.removeEventListener('copy', this.handleEvent);
        document.removeEventListener('paste', this.handleEvent);
        document.removeEventListener('contextmenu', this.handleEvent);
        document.removeEventListener('visibilitychange', this.handleVisibility);
        document.removeEventListener('fullscreenchange', this.handleFullscreen);
        window.removeEventListener('blur', this.handleBlur);
        window.removeEventListener('focus', this.handleFocus);
        window.removeEventListener('focus', this.handleFocus);
        document.removeEventListener('keydown', this.handleKey);
        document.removeEventListener('keyup', this.handleKeyUp);
        this.dotNetRef = null;
        this.hideToast();
    },

    handleEvent: function (e) {
        if (e.type === 'copy' || e.type === 'paste' || e.type === 'contextmenu') {
            e.preventDefault();
            window.proctoring.showToast('Kopya engellendi');
        }
        if (window.proctoring.dotNetRef) {
            let msg = e.type;
            if (e.type === 'copy') msg = 'Kopyalama Denemesi';
            if (e.type === 'paste') msg = 'Yapıştırma Denemesi';
            if (e.type === 'contextmenu') msg = 'Sağ Tık Denemesi';

            window.proctoring.dotNetRef.invokeMethodAsync('OnProctorEvent', {
                type: msg,
                content: 'Engellendi',
                timestampUtc: new Date().toISOString()
            });
        }
    },

    handleVisibility: function () {
        if (window.proctoring.dotNetRef) {
            const msg = document.hidden ? 'Sekme Alta Alındı / Gizlendi' : 'Sekme Tekrar Açıldı';
            window.proctoring.dotNetRef.invokeMethodAsync('OnProctorEvent', {
                type: 'Sekme Durumu',
                content: msg,
                timestampUtc: new Date().toISOString()
            });
        }
    },

    handleFullscreen: function () {
        if (window.proctoring.dotNetRef) {
            const isFull = document.fullscreenElement !== null;
            const msg = isFull ? 'Tam Ekrana Geçildi' : 'Tam Ekrandan Çıkıldı (Force F11)';
            window.proctoring.dotNetRef.invokeMethodAsync('OnProctorEvent', {
                type: 'Ekran Durumu',
                content: msg,
                timestampUtc: new Date().toISOString()
            });
        }
    },

    handleBlur: function () {
        if (window.proctoring.dotNetRef) {
            window.proctoring.dotNetRef.invokeMethodAsync('OnProctorEvent', {
                type: 'Odak Durumu',
                content: 'Pencere Odak Kaybı (Başka uygulamaya geçildi)',
                timestampUtc: new Date().toISOString()
            });
        }
    },

    handleFocus: function () {
        if (window.proctoring.dotNetRef) {
            window.proctoring.dotNetRef.invokeMethodAsync('OnProctorEvent', {
                type: 'Odak Durumu',
                content: 'Pencere Odağı Geri Geldi',
                timestampUtc: new Date().toISOString()
            });
        }
    },

    handleKey: function (e) {
        if (window.proctoring.isKeyDown) return;

        const isCopy = e.ctrlKey && (e.key === 'c' || e.key === 'C');
        const isPaste = e.ctrlKey && (e.key === 'v' || e.key === 'V');
        const isF12 = e.key === 'F12';

        if (isCopy || isPaste || isF12) {
            e.preventDefault();
            window.proctoring.isKeyDown = true;

            const msg = isF12 ? 'Geliştirici araçları engellendi' : 'Kopya engellendi';
            window.proctoring.showToast(msg);

            if (window.proctoring.dotNetRef) {
                let type = 'Kısayol Engellendi';
                if (isCopy) type = 'Kopyalama Denemesi';
                if (isPaste) type = 'Yapıştırma Denemesi';
                if (isF12) type = 'Geliştirici Araçları';

                window.proctoring.dotNetRef.invokeMethodAsync('OnProctorEvent', {
                    type: type,
                    content: isF12 ? 'F12 Tuşu Engellendi' : 'Kısayol Tuşu Engellendi',
                    timestampUtc: new Date().toISOString()
                });
            }
        }
    },

    handleKeyUp: function (e) {
        window.proctoring.isKeyDown = false;
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
