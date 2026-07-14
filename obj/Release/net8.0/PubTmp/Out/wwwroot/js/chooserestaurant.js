
 


$(document).ready(function () {
    // Show/hide add restaurant form
    $("#btnShowAdd").click(function () {
        $("#addRestaurantForm").slideToggle();
        $("#restaurantName").focus();
    });

    // Submit via button click
    $("#btnAddRestaurant").click(function () {
        addRestaurant();
    });

    // Submit via Enter key
    $("#restaurantName").keypress(function (e) {
        if (e.which === 13) {
            e.preventDefault();
            addRestaurant();
        }
    });

    // Helper: redirect after adding
    function redirectAfterAdd(restaurantId) {
        if (restaurantId) {
            window.location.href = '/Home/Dashboard/' + restaurantId;
        } else {
            location.reload();
        }
    }

    // Show Free Trial modal
    function showFreeTrialModal(message, onClose) {
        var modalHtml =
            '<div class="modal fade" id="freeTrialModal" tabindex="-1">' +
            '  <div class="modal-dialog modal-dialog-centered">' +
            '    <div class="modal-content text-center rounded-4 shadow border-0 p-3">' +
            '      <div class="modal-body">' +
            '        <div class="mb-3"><i class="fa-solid fa-gift fa-3x text-orange"></i></div>' +
            '        <h5 class="fw-bold text-orange mb-2">تبریک! 🎉</h5>' +
            '        <p class="mb-3">' + message + '</p>' +
            '        <button type="button" class="btn btn-orange w-100 fw-bold" id="closeTrialModalBtn">متوجه شدم</button>' +
            '      </div>' +
            '    </div>' +
            '  </div>' +
            '</div>';

        $("#freeTrialModal").remove();
        $("body").append(modalHtml);

        var modalEl = document.getElementById("freeTrialModal");
        var modal = new bootstrap.Modal(modalEl);
        modal.show();

        $("#closeTrialModalBtn").on("click", function () {
            modal.hide();
        });

        modalEl.addEventListener("hidden.bs.modal", function () {
            $(this).remove();
            if (onClose) onClose();
        });
    }

    // Main function: add restaurant using fetchWithAuth
    async function addRestaurant() {
        var restaurantName = $("#restaurantName").val().trim();
        var errorDiv = $("#nameError");

        errorDiv.hide().text('');

        if (!restaurantName) {
            errorDiv.text('نام رستوران الزامی است').show();
            $("#restaurantName").focus();
            return;
        }
        if (restaurantName.length < 2) {
            errorDiv.text('نام رستوران باید حداقل ۲ کاراکتر باشد').show();
            $("#restaurantName").focus();
            return;
        }

        var $btn = $("#btnAddRestaurant");
        $btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm"></span> در حال افزودن...');

        var payload = { name: restaurantName };

        try {
            // ✅ Use fetchWithAuth (interceptor handles token refresh)
            const response = await window.fetchWithAuth('/api/v2/UserApi/addrestaurant', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            const res = await response.json();

            if (response.ok && res.success) {
                if (res.has_free_trial) {
                    showFreeTrialModal(res.message, function () {
                        redirectAfterAdd(res.restaurant_id);
                    });
                } else {
                    alert("✅ " + res.message);
                    setTimeout(function () {
                        redirectAfterAdd(res.restaurant_id);
                    }, 1500);
                }
            } else {
                // Server returned error (non-200 or success false)
                alert("❌ " + (res.message || 'خطا در ثبت رستوران'));
                errorDiv.text(res.message || 'خطا').show();
            }
        } catch (error) {
            // Network errors or interceptor redirection (e.g., refresh failure)
            // The interceptor already redirects to login on auth failure, so we just show a generic error.
            alert("❌ خطا در ارتباط با سرور: " + (error.message || ''));
            errorDiv.text('خطا در ارتباط با سرور').show();
        } finally {
            $btn.prop('disabled', false).text('افزودن');
        }
    }

    // Expose addRestaurant globally if needed (not required)
});
 