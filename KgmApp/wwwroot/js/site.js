// Client-side search + pagination for responsive tables
window.kgmInitTablePage = function (options) {
    const table = document.getElementById(options.tableId);
    const searchInput = document.getElementById(options.searchInputId);
    const paginationEl = document.getElementById(options.paginationId);
    const infoEl = document.getElementById(options.infoId);
    const pageSize = options.pageSize || 5;

    if (!table || !searchInput || !paginationEl) return;

    const tbody = table.querySelector("tbody");
    if (!tbody) return;

    let filtered = [];
    let currentPage = 1;

    function rowText(tr) {
        return tr.innerText.replace(/\s+/g, " ").trim().toLowerCase();
    }

    function applyFilter() {
        const q = searchInput.value.trim().toLowerCase();
        const all = Array.from(tbody.querySelectorAll("tr"));
        filtered = q ? all.filter((tr) => rowText(tr).includes(q)) : all.slice();
        currentPage = 1;
        render();
    }

    function render() {
        const all = Array.from(tbody.querySelectorAll("tr"));
        all.forEach((tr) => {
            tr.style.display = "none";
        });

        const total = filtered.length;
        const pageCount = Math.max(1, Math.ceil(total / pageSize));

        if (currentPage > pageCount) currentPage = pageCount;

        const start = (currentPage - 1) * pageSize;
        const slice = filtered.slice(start, start + pageSize);
        slice.forEach((tr) => {
            tr.style.display = "";
        });

        if (infoEl) {
            if (total === 0) {
                infoEl.textContent = "No matching rows.";
            } else {
                const from = total === 0 ? 0 : start + 1;
                const to = Math.min(start + pageSize, total);
                infoEl.textContent = "Showing " + from + "–" + to + " of " + total;
            }
        }

        paginationEl.innerHTML = "";
        if (pageCount <= 1) return;

        function addItem(label, page, disabled) {
            const li = document.createElement("li");
            li.className = "page-item" + (disabled ? " disabled" : "");
            const a = document.createElement("a");
            a.className = "page-link rounded-2";
            a.href = "#";
            a.textContent = label;
            a.setAttribute("aria-label", label);
            if (!disabled) {
                a.addEventListener("click", function (e) {
                    e.preventDefault();
                    currentPage = page;
                    render();
                });
            }
            li.appendChild(a);
            paginationEl.appendChild(li);
        }

        addItem("«", currentPage - 1, currentPage <= 1);
        for (let p = 1; p <= pageCount; p++) {
            const li = document.createElement("li");
            li.className = "page-item" + (p === currentPage ? " active" : "");
            const a = document.createElement("a");
            a.className = "page-link rounded-2";
            a.href = "#";
            a.textContent = String(p);
            if (p !== currentPage) {
                a.addEventListener("click", function (e) {
                    e.preventDefault();
                    currentPage = p;
                    render();
                });
            }
            li.appendChild(a);
            paginationEl.appendChild(li);
        }
        addItem("»", currentPage + 1, currentPage >= pageCount);
    }

    searchInput.addEventListener("input", applyFilter);
    applyFilter();
};

// Sidebar behavior: desktop collapse + mobile overlay
(function () {
    const body = document.body;
    const desktopToggle = document.getElementById("sidebarDesktopToggle");
    const mobileToggle = document.getElementById("sidebarMobileToggle");
    const overlay = document.getElementById("appOverlay");

    if (!body) return;

    function closeMobileSidebar() {
        body.classList.remove("sidebar-mobile-open");
    }

    if (desktopToggle) {
        desktopToggle.addEventListener("click", function () {
            body.classList.toggle("sidebar-collapsed");
        });
    }

    if (mobileToggle) {
        mobileToggle.addEventListener("click", function () {
            body.classList.toggle("sidebar-mobile-open");
        });
    }

    if (overlay) {
        overlay.addEventListener("click", closeMobileSidebar);
    }

    window.addEventListener("resize", function () {
        if (window.innerWidth >= 992) {
            closeMobileSidebar();
        }
    });
})();
