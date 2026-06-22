 

var currentOwnerId = parseInt(currentOwnerId) || 0;



    $(document).ready(function () {
        // نمایش/مخفی کردن فرم افزودن رستوران
        $("#btnShowAdd").click(function () {
            $("#addRestaurantForm").slideToggle();
            $("#restaurantName").focus();
        });

    // ارسال درخواست افزودن رستوران
    $("#btnAddRestaurant").click(function () {
        addRestaurant();
            });

    // امکان ارسال با کلید Enter
    $("#restaurantName").keypress(function (e) {
                if (e.which === 13) {
        e.preventDefault();
    addRestaurant();
                }
    });
        function redirectAfterAdd(restaurantId) {
            if (restaurantId) {
                window.location.href = '/Home/Dashboard/' + restaurantId;
            } else {
                location.reload();
            }
        }

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

    function addRestaurant() {
                var restaurantName = $("#restaurantName").val().trim();
    var errorDiv = $("#nameError");

    // ریست خطاها
    errorDiv.hide().text('');

    // اعتبارسنجی
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

    // دکمه غیرفعال شود
    var $btn = $("#btnAddRestaurant");
    $btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm"></span> در حال افزودن...');

    // ساخت payload - استفاده از ViewBag.OwnerId
    var payload = {
        name: restaurantName,
    owner_id: currentOwnerId // مقدار پیش‌فرض برای جلوگیری از خطا
                };

    // بررسی owner_id
    if (payload.owner_id === 0) {
        alert("خطا: شناسه مالک نامعتبر است");
    $btn.prop('disabled', false).text('افزودن');
    return;
                }

  
    $.ajax({
        url: "/api/userapi/addrestaurant",  
    type: "POST",
    contentType: "application/json",
    headers: {
        
            
        'X-Requested-With': 'XMLHttpRequest'
                    },
    data: JSON.stringify(payload),
        success: function (res) {
            if (res.success) {
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
                alert("❌ " + res.message);
                errorDiv.text(res.message).show();
            }
        }, 
    error: function (xhr, status, error) {
                        var errorMessage = "خطا در ارتباط با سرور";
    try {
                            var response = JSON.parse(xhr.responseText);
    if (response && response.message) {
        errorMessage = response.message;
                            }
                        } catch (e) {
        // اگر response JSON نبود
    }
    alert("❌ " + errorMessage);
    errorDiv.text(errorMessage).show();
    console.error("Error:", error);
                    },
    complete: function () {
        // فعال کردن دکمه
        $btn.prop('disabled', false).text('افزودن');
                    }
                });
            }
        });
 