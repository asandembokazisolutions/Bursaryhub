// ─── Utility Functions ───────────────────────────────────────────────────────

// Toggle password visibility
function togglePassword(inputId, btnId) {
    const input = document.getElementById(inputId);
    const btn = document.getElementById(btnId);
    if (input.type === 'password') {
        input.type = 'text';
        if (btn) btn.innerHTML = '<i class="bi bi-eye-slash"></i>';
    } else {
        input.type = 'password';
        if (btn) btn.innerHTML = '<i class="bi bi-eye"></i>';
    }
}

// Auto-dismiss alerts after 5 seconds
document.addEventListener('DOMContentLoaded', function () {
    const alerts = document.querySelectorAll('.alert');
    alerts.forEach(alert => {
        if (alert.classList.contains('alert-success') || alert.classList.contains('alert-info')) {
            setTimeout(() => {
                alert.style.opacity = '0';
                alert.style.transition = 'opacity 0.3s ease';
                setTimeout(() => alert.remove(), 300);
            }, 5000);
        }
    });

    // Enable Bootstrap tooltips if present
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });
});

// Form validation feedback
const forms = document.querySelectorAll('.needs-validation');
Array.from(forms).forEach(form => {
    form.addEventListener('submit', event => {
        if (!form.checkValidity()) {
            event.preventDefault();
            event.stopPropagation();
        }
        form.classList.add('was-validated');
    }, false);
});

// Confirm delete actions
function confirmDelete(msg) {
    return confirm(msg || 'Are you sure? This action cannot be undone.');
}

// Format currency display
function formatCurrency(value) {
    return new Intl.NumberFormat('en-ZA', {
        style: 'currency',
        currency: 'ZAR',
        minimumFractionDigits: 0
    }).format(value);
}

// Clone form row (for repeating fields if needed)
function cloneFormRow(parentSelector) {
    const parent = document.querySelector(parentSelector);
    if (!parent) return;
    const lastRow = parent.lastElementChild;
    const cloned = lastRow.cloneNode(true);
    parent.appendChild(cloned);
}

// Log helper for debugging
function log(msg, level = 'info') {
    console.log(`[${level.toUpperCase()}] ${msg}`);
}
