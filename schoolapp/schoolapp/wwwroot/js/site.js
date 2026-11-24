document.addEventListener("DOMContentLoaded", () => {
    const menuButton = document.querySelector(".menu-toggle");
    const sidebar = document.querySelector(".hms-sidebar");

    if (menuButton && sidebar) {
        menuButton.addEventListener("click", () => {
            const isOpen = sidebar.classList.toggle("is-open");
            menuButton.setAttribute("aria-expanded", String(isOpen));
        });

        sidebar.addEventListener("click", (event) => {
            if (event.target.closest("a") && window.matchMedia("(max-width: 860px)").matches) {
                sidebar.classList.remove("is-open");
                menuButton.setAttribute("aria-expanded", "false");
            }
        });
    }

    document.querySelectorAll("form[data-confirm]").forEach((form) => {
        form.addEventListener("submit", (event) => {
            if (!window.confirm(form.dataset.confirm)) {
                event.preventDefault();
            }
        });
    });
});
