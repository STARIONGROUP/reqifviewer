window.matrixScroll = (() => {
    const debounceMs = 250;
    // Wall-clock budget for the restore retry. The matrix builds asynchronously and
    // Virtualize sizes its scroll spacers lazily over several frames (longer on a big
    // ReqIF), so the container is not tall enough to honor a large scrollTop right
    // away. We keep re-asserting until it is, bounded so we never loop forever.
    const restoreBudgetMs = 4000;
    const handlers = new Map(); // element -> { listener, key }

    function restore(element, targetTop, targetLeft) {
        if (targetTop === 0 && targetLeft === 0) {
            return;
        }

        const deadline = performance.now() + restoreBudgetMs;
        let goodFrames = 0;

        const step = () => {
            const maxTop = Math.max(0, element.scrollHeight - element.clientHeight);
            const maxLeft = Math.max(0, element.scrollWidth - element.clientWidth);

            element.scrollTop = Math.min(targetTop, maxTop);
            element.scrollLeft = Math.min(targetLeft, maxLeft);

            // Only count as settled once the container can actually accommodate the
            // target (Virtualize has sized its spacers) AND the browser accepted the
            // assignment. Re-assert for a few extra frames afterwards because
            // Virtualize re-renders its row window on the programmatic scroll and can
            // nudge layout once more.
            const heightReady = maxTop >= targetTop - 1;
            const widthReady = maxLeft >= targetLeft - 1;
            const topApplied = Math.abs(element.scrollTop - targetTop) <= 1;
            const leftApplied = Math.abs(element.scrollLeft - targetLeft) <= 1;

            if (heightReady && widthReady && topApplied && leftApplied) {
                goodFrames++;
            } else {
                goodFrames = 0;
            }

            if (goodFrames < 3 && performance.now() < deadline) {
                requestAnimationFrame(step);
            }
        };

        requestAnimationFrame(step);
    }

    function attach(element, key, rowHeight, cellWidth, rowIds, colIds) {
        if (!element || !rowHeight || !cellWidth) {
            return;
        }

        const previous = handlers.get(element);
        if (previous) {
            if (previous.key === key) {
                // Already wired for this view — do not re-restore or duplicate the
                // listener, otherwise re-renders would yank the viewport.
                return;
            }
            element.removeEventListener('scroll', previous.listener);
        }

        const rows = rowIds || [];
        const cols = colIds || [];

        const params = new URL(window.location.href).searchParams;
        const savedRow = Math.max(0, rows.indexOf(params.get('anchorRow')));
        const savedCol = Math.max(0, cols.indexOf(params.get('anchorCol')));
        restore(element, savedRow * rowHeight, savedCol * cellWidth);

        let timer = null;
        const listener = () => {
            if (timer) {
                clearTimeout(timer);
            }
            timer = setTimeout(() => {
                if (rows.length === 0 || cols.length === 0) {
                    return;
                }

                const row = Math.min(rows.length - 1, Math.max(0, Math.floor(element.scrollTop / rowHeight)));
                const col = Math.min(cols.length - 1, Math.max(0, Math.floor(element.scrollLeft / cellWidth)));

                const url = new URL(window.location.href);
                url.searchParams.set('anchorRow', rows[row]);
                url.searchParams.set('anchorCol', cols[col]);
                // replaceState (not pushState) so scrolling adds no history entries;
                // pass history.state through so Blazor's client router state survives.
                history.replaceState(history.state, '', url);
            }, debounceMs);
        };
        element.addEventListener('scroll', listener, { passive: true });
        handlers.set(element, { listener, key });
    }

    function detach(element) {
        const previous = handlers.get(element);
        if (!previous) {
            return;
        }
        element.removeEventListener('scroll', previous.listener);
        handlers.delete(element);
    }

    // ---- viewport fitting -------------------------------------------------
    // Size the wrapper so it exactly fills the space between its own top and
    // the bottom of the viewport (above the Radzen footer). Component-local:
    // no global CSS / body class. Recomputed on resize + parent reflow.
    const fitGap = 8;
    const fitters = new Map(); // element -> { onResize, ro }

    function applyHeight(element) {
        const rect = element.getBoundingClientRect();
        // Distance from the document top — stable regardless of any current
        // page scroll, so the value converges (no measurement feedback loop).
        const absoluteTop = rect.top + window.scrollY;
        const footer = document.querySelector('.rz-footer');
        const footerH = footer ? footer.getBoundingClientRect().height : 0;
        const height = Math.max(120, window.innerHeight - absoluteTop - footerH - fitGap);
        element.style.height = height + 'px';
    }

    function fit(element) {
        if (!element) {
            return;
        }

        if (fitters.has(element)) {
            applyHeight(element);
            return;
        }

        let scheduled = false;
        const onResize = () => {
            if (scheduled) {
                return;
            }
            scheduled = true;
            requestAnimationFrame(() => {
                scheduled = false;
                applyHeight(element);
            });
        };

        requestAnimationFrame(() => applyHeight(element));
        // Catch late Radzen / web-font layout shifts.
        setTimeout(() => applyHeight(element), 100);

        window.addEventListener('resize', onResize, { passive: true });

        let ro = null;
        if (typeof ResizeObserver !== 'undefined' && element.parentElement) {
            ro = new ResizeObserver(onResize);
            ro.observe(element.parentElement);
        }

        fitters.set(element, { onResize, ro });
    }

    function unfit(element) {
        const entry = fitters.get(element);
        if (!entry) {
            return;
        }
        window.removeEventListener('resize', entry.onResize);
        if (entry.ro) {
            entry.ro.disconnect();
        }
        element.style.height = '';
        fitters.delete(element);
    }

    return { attach, detach, fit, unfit };
})();
