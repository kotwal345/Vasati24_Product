// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(function () {
    var toggle = document.getElementById('sidebarToggle');
    var close = document.getElementById('sidebarClose');
    var backdrop = document.getElementById('sidebarBackdrop');
    if (!toggle) return;

    function setOpen(open) {
        document.body.classList.toggle('sidebar-open', open);
        toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    }

    toggle.addEventListener('click', function () {
        setOpen(!document.body.classList.contains('sidebar-open'));
    });
    if (close) close.addEventListener('click', function () { setOpen(false); });
    if (backdrop) backdrop.addEventListener('click', function () { setOpen(false); });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') setOpen(false);
    });
})();

// Disables a form's submit button (and shows a spinner) the moment it's submitted, so a slow
// request or an impatient double-click can't fire a Create/Deactivate/Reactivate action twice.
// Opt in per-form with data-disable-on-submit; skipped if client-side validation blocks the
// submit, so a fixable validation error never leaves the button stuck disabled.
(function () {
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!(form instanceof HTMLFormElement) || !form.hasAttribute('data-disable-on-submit')) return;
        if (form.checkValidity && !form.checkValidity()) return;

        var button = form.querySelector('button[type="submit"]');
        if (!button || button.disabled) return;

        button.disabled = true;
        button.dataset.originalText = button.innerHTML;
        button.innerHTML = '<span class="btn-spinner" aria-hidden="true"></span>' + button.textContent;
    });
})();
