 // Podium JS: handles mounting animation variant, reduced-motion, and confetti single run.
// Usage: window.PodiumInit({variant: "pop", reducedMotion: false});

(function (global) {
    let confettiRaf = null;
    let confettiTimeout = null;

    function PodiumInit(opts = {}) {
        const root = document.getElementById('podium-root');
        if (!root) return;
        const variant = opts.variant || root.dataset.variant || "pop";
        const reduced = !!opts.reducedMotion;

        // Apply animation class unless reduced-motion
        if (reduced) {
            root.classList.remove('podium-animate');
            root.classList.remove('pop', 'subtle-slide', 'slow-fade');
        } else {
            // add animate class + variant
            root.classList.add('podium-animate', variant);
        }

        // Trigger one-time confetti
        if (!reduced) startConfettiOnce();

        // Accessibility: keyboard focus styling already supported. Ensure aria-live announcement:
        const players = root.querySelectorAll('[role="listitem"]');
        setTimeout(() => {
            players.forEach((el, idx) => {
                // small delay to ensure animation runs then set aria-label
                el.setAttribute('aria-hidden', 'false');
            });
        }, 700);
    }

    // Confetti: lightweight canvas, low particle count, 1 - 1.5s
    function startConfettiOnce() {
        const canvas = document.getElementById('confetti-canvas');
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        const DPR = window.devicePixelRatio || 1;
        function resize() { canvas.width = canvas.clientWidth * DPR; canvas.height = canvas.clientHeight * DPR; ctx.scale(DPR, DPR); }
        resize();
        window.addEventListener('resize', resize);

        const particles = [];
        const count = 18; // low density
        for (let i = 0; i < count; i++) {
            particles.push({
                x: Math.random() * canvas.clientWidth,
                y: -10 - Math.random() * 40,
                vx: (Math.random() - 0.5) * 2,
                vy: 2 + Math.random() * 2,
                size: 4 + Math.random() * 6,
                rot: Math.random() * 360,
                color: ['#FFD54A', '#B0BEC5', '#FFAB91'][Math.floor(Math.random() * 3)],
                life: 1000 + Math.random() * 400
            });
        }

        let start = performance.now();
        function loop(t) {
            const elapsed = t - start;
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            // draw
            for (const p of particles) {
                p.x += p.vx;
                p.y += p.vy;
                p.vy += 0.03;
                ctx.save();
                ctx.translate(p.x, p.y);
                ctx.rotate(p.rot * Math.PI / 180);
                ctx.fillRect(-p.size / 2, -p.size / 2, p.size, p.size * 1.2);
                ctx.restore();
            }
            if (elapsed < 1600) confettiRaf = requestAnimationFrame(loop);
            else {
                cancelAnimationFrame(confettiRaf);
                ctx.clearRect(0, 0, canvas.width, canvas.height);
                window.removeEventListener('resize', resize);
            }
        }
        confettiRaf = requestAnimationFrame(loop);
    }

    // Expose
    global.window.PodiumInit = PodiumInit;
})(window);
