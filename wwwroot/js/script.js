
//Hiển thị thông báo
function showMessage(message, type, target) {
    // Nếu không truyền target -> mặc định dùng #message (code cũ)
    if (!target) {
        target = "#message";
    }

    let messageDiv = $(target);

    // Nếu target không tồn tại trên trang thì dùng popup góc phải
    if (!messageDiv.length) {
        if (!$('#global-alert-container').length) {
            $('body').append('<div id="global-alert-container" style="position:fixed;top:20px;right:20px;z-index:2000;max-width:420px;width:calc(100% - 40px);"></div>');
        }
        target = '#global-alert-container';
        messageDiv = $(target);
    }

    const alertItem = $(`
        <div class="alert alert-${type} alert-dismissible fade show mb-2" role="alert" style="box-shadow:0 10px 28px rgba(0,0,0,.18);">
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>
    `);

    if (target === '#global-alert-container') {
        messageDiv.prepend(alertItem).fadeIn();
        setTimeout(function () {
            alertItem.fadeOut(250, function () {
                $(this).remove();
            });
        }, 2600);
        return;
    }

    messageDiv.html(alertItem).fadeIn();
    setTimeout(function () {
        messageDiv.fadeOut();
    }, 2600);
}

function getInputPrimaryFile(inputOrSelector) {
    const input = typeof inputOrSelector === 'string' ? $(inputOrSelector)[0] : inputOrSelector;
    if (!input) {
        return null;
    }

    return (input.files && input.files.length > 0)
        ? input.files[0]
        : (input._droppedFile || null);
}

function appendInputFileIfAny(formData, fieldName, inputOrSelector) {
    const file = getInputPrimaryFile(inputOrSelector);
    if (file) {
        formData.append(fieldName, file);
    }
}

// =========================================================================
// SỰ KIỆN ADMIN
// =========================================================================
$(function () {

    // Đăng nhập Admin
    $(document).on("submit", "#adminLoginForm", function (e) {
        e.preventDefault(); // Ngăn form submit mặc định

        let username = $("#Username").val().trim();
        let password = $("#Password").val().trim();
        let rememberMe = $("#RememberMe").is(":checked");

        if (username === "") {
            alert("Vui lòng nhập tài khoản!");
            return;
        }
        if (password === "") {
            alert("Vui lòng nhập mật khẩu!");
            return;
        }

        $.ajax({
            url: "/Admin/Auth/Login",
            type: "POST",
            contentType: "application/json",
            data: JSON.stringify({
                Username: username,
                Password: password,
                RememberMe: rememberMe
            }),
            success: function (response) {
                console.log("Phản hồi từ server:", response);
                if (response.success) {
                    
                    showMessage(response.message, "success");
                    setTimeout(function () {
                        window.location.href = "/Admin/Dashboard";
                    }, 2000);
                } else {
                    showMessage(response.message, "danger");
                }
            },
            error: function (xhr) {
                console.log("Lỗi AJAX:", xhr.responseText);
                alert("Đã xảy ra lỗi! Vui lòng thử lại.");
            }
        });
    });

    // Chỉnh sửa Admin
    $(document).on("click", "#saveAdminBtn", function (e) {
        e.preventDefault();
        let formData = new FormData();
        formData.append("Id", $("#Id").val());
        formData.append("Username", $("#Username").val());
        formData.append("FullName", $("#FullName").val());
        formData.append("Roles", $("#Roles").val());
        formData.append("Status", $("#Status").val());
        formData.append("CreationDate", $("#CreationDate").val());
        let oldPassword = $("#OldPassword").val().trim();
        let newPassword = $("#NewPassword").val().trim();
        if (oldPassword !== "") {
            formData.append("OldPassword", oldPassword);
        }
        if (newPassword !== "") {
            formData.append("NewPassword", newPassword);
        }
        appendInputFileIfAny(formData, "AvatarFile", "#AvatarFile");

        // Hiển thị thông báo đang xử lý
        $("#message").html('<div class="alert alert-info">Đang xử lý...</div>');

        $.ajax({
            url: "/admin/edit/" + $("#Id").val(),
            type: "POST",
            processData: false,
            contentType: false,
            data: formData,
            success: function (response) {
                console.log("Phản hồi từ server:", response);
                if (response.success) {
                    $("#message").html('<div class="alert alert-success">' + response.message + '</div>');

                    if (response.needRefresh) {
                        // Nếu cần refresh (trường hợp cập nhật chính user đang đăng nhập)
                        setTimeout(function () {
                            // Chuyển đến trang dashboard và bắt buộc reload
                            window.location.href = "/admin/dashboard";
                            // Thêm timestamp để tránh cache
                            window.location.href = "/admin/dashboard?t=" + new Date().getTime();
                        }, 1500);
                    } else {
                        // Trường hợp bình thường, chuyển đến danh sách admin
                        setTimeout(function () {
                            window.location.href = "/admin/list-admins";
                        }, 2000);
                    }
                } else {
                    $("#message").html('<div class="alert alert-danger">' + response.message + '</div>');
                }
            },
            error: function (xhr) {
                console.log("Lỗi AJAX:", xhr.responseText);
                $("#message").html('<div class="alert alert-danger">Lỗi: ' + xhr.responseText + '</div>');
            }
        });
    });

    //Thêm Admin
    $(document).on("click", "#saveNewAdminBtn", function (e) {
        e.preventDefault();

        let formData = new FormData();
        formData.append("Username", $("#NewUsername").val());
        formData.append("FullName", $("#NewFullName").val());
        formData.append("Roles", $("#NewRoles").val());
        formData.append("Password", $("#NewPassword").val());
        formData.append("Status", $("#NewStatus").val());

        appendInputFileIfAny(formData, "AvatarFile", "#NewAvatar");

        $.ajax({
            url: "/admin/add-admin",
            type: "POST",
            data: formData,
            contentType: false,
            processData: false,
            success: function (response) {
                if (response.success) {
                    showMessage(response.message, "success");

                    // Tạo hàng mới để thêm vào bảng
                    let newRow = `
                            <tr>
                                <td class="avatar">
                                    <img src="${response.avatarUrl}" alt="Ảnh đại diện" />
                                </td>
                                <td>${$("#NewUsername").val()}</td>
                                <td class="title">
                                    <span>${$("#NewFullName").val()}</span>
                                </td>
                                <td class="create-date">
                                    <span>${new Date().toLocaleDateString('vi-VN')}</span>
                                </td>
                                <td class="status">
                                    <span class="${$("#NewStatus").val() === 'Hoạt động' ? 'active' : 'inactive'}">${$("#NewStatus").val()}</span>
                                </td>
                                <td>${$("#NewRoles").val()}</td>
                                <td class="action">
                                    <a href="/admin/edit/${response.adminId}">
                                        <i class="ri-pencil-line"></i>
                                    </a>
                                    <a href="javascript:void(0);" class="delete-admin" data-id="${response.adminId}">
                                        <i class="ri-delete-bin-6-line"></i>
                                    </a>
                                </td>
                            </tr>
                        `;

                    // Thêm hàng mới vào đầu bảng
                    $("table.list tbody").append(newRow);

                    // Reset form và đóng modal
                    $("#addAdminForm")[0].reset();
                    const newAvatarInput = $("#NewAvatar")[0];
                    if (newAvatarInput) {
                        newAvatarInput._droppedFile = null;
                    }
                    $("#newAvatarPreview").attr("src", "/avatar/default-avatar.png");
                    $("#addAdminModal").modal("hide");
                } else {
                    $showMessage(response.message, "danger");
                }
            },
            error: function () {
                alert("Lỗi khi gửi yêu cầu!");
            }
        });
    });

    //Xóa admin
    $(document).on("click", ".delete-admin", function (e) {
        e.preventDefault();
        let adminId = $(this).data("id");

        if (!confirm("Bạn có chắc chắn muốn xóa admin này?")) return;

        $.ajax({
            url: "/admin/delete/" + adminId,
            type: "POST",
            success: function (response) {
                showMessage(response.message, "success");

                if (response.success) {
                    // Xóa hàng chứa Admin đã bị xóa
                    $("tr").filter(function () {
                        return $(this).find(".delete-admin").data("id") == adminId;
                    }).remove();
                } else {
                    showMessage(response.message, "danger");
                }
            }
        });
    });

    //Thêm danh mục
    $(document).on("click", "#saveNewCategoryBtn", function (e) {
        e.preventDefault();

        let categoryName = $("#NewCategoryName").val();

        console.log("CategoryName:", categoryName);

        let formData = new FormData();
        formData.append("CategoryName", categoryName);

        $.ajax({
            url: "/category/add-category",
            type: "POST",
            data: formData,
            contentType: false,
            processData: false,
            success: function (response) {
                console.log("Server response:", response);
                if (response.success) {
                    let newRow = `
                        <tr>
                            <td class="title"><span>${response.categoryName}</span></td>
                            <td class="create-date"><span>${response.creationDate}</span></td>
                            <td class="update-date"><span>${response.updationDate}</span></td> <!-- Sử dụng giá trị từ server -->
                            <td>${response.adminName}</td>
                            <td class="action">
                                <a href="/category/edit-category/${response.categoryId}">
                                    <i class="ri-pencil-line"></i>
                                </a>
                                <a href="javascript:void(0);" class="delete-category" data-id="${response.categoryId}">
                                    <i class="ri-delete-bin-6-line"></i>
                                </a>
                            </td>
                        </tr>`;
                    $("#categoryTable tbody").prepend(newRow);

                    $('#addCategoryModal').modal('hide');
                    $("#NewCategoryName").val('');
                    showMessage(response.message, "success");
                } else {
                    showMessage(response.message, "danger");
                }
            },
            error: function (xhr, status, error) {
                console.log("AJAX Error:", xhr.responseText);
                alert("Lỗi khi gửi yêu cầu!");
            }
        });
    });

    //Chỉnh sửa danh mục 
    $(document).on("click", "#saveCategoryBtn", function (e) {
        e.preventDefault();

        let formData = new FormData();
        formData.append("Id", $("#Id").val());
        formData.append("CategoryName", $("#CategoryName").val());

        $.ajax({
            url: "/category/edit-category/" + $("#Id").val(),
            type: "POST",
            processData: false,
            contentType: false,
            data: formData,
            success: function (response) {
                console.log("Phản hồi từ server:", response);
                if (response.success) {
                    showMessage(response.message, "success");
                    setTimeout(() => window.location.href = "/category/list-category", 2000);
                } else {
                    $("#message").html('<div class="alert alert-danger">' + response.message + '</div>');
                }
            },
            error: function (xhr) {
                console.log("Lỗi AJAX:", xhr.responseText);
                showMessage(response.message, "danger");
            }
        });
    });

    // Xóa danh mục
    $(document).on("click", ".delete-category", function (e) {
        e.preventDefault();

        let categoryId = $(this).data("id");
        if (!confirm("Bạn có chắc chắn muốn xóa danh mục này?")) return;

        $.ajax({
            url: "/category/delete-category/" + categoryId,
            type: "POST",
            success: function (response) {
                console.log("Server response:", response);
                if (response.success) {
                    $(`a.delete-category[data-id="${categoryId}"]`).closest("tr").remove();
                    showMessage(response.message, "success");
                } else {
                    showMessage(response.message, "danger");
                }
            },
            error: function (xhr, status, error) {
                console.log("AJAX Error:", xhr.responseText);
                alert("Lỗi khi gửi yêu cầu!");
            }
        });
    });

    //Thêm danh mục con
    $(document).on("click", "#saveNewSubCategoryBtn", function (e) {
        e.preventDefault();
        var categoryId = $("#CategoryId").val();
        var subCategoryName = $("#NewSubCategoryName").val();

        if (!categoryId || !subCategoryName) {
            alert("Vui lòng nhập đầy đủ thông tin!");
            return;
        }

        $.ajax({
            url: "/subcategory/add-subcategory",
            type: "POST",
            data: {
                CategoryId: categoryId,
                SubCategoryName: subCategoryName
            },
            success: function (response) {
                if (response.success) {
                    showMessage(response.message, "success");

                    // Tạo hàng mới để thêm vào bảng
                    var newRow = `
                            <tr>
                                <td class="title">
                                    <span>${response.subCategoryName}</span>
                                </td>
                                <td>${response.categoryName}</td>
                                <td class="create-date">
                                    <span>${response.creationDate}</span>
                                </td>
                                <td class="update-date">
                                    <span class="text-danger">${response.updationDate}</span>
                                </td>
                                <td class="action">
                                    <a href="/subcategory/editsubcategory/${response.subCategoryId}">
                                        <i class="ri-pencil-line"></i>
                                    </a>
                                    <a href="javascript:void(0);" class="delete-subcategory" data-id="${response.subCategoryId}">
                                        <i class="ri-delete-bin-6-line"></i>
                                    </a>
                                </td>
                            </tr>
                        `;

                    // Thêm hàng mới vào đầu bảng
                    $("table.list tbody").prepend(newRow);

                    // Reset form và đóng modal
                    $("#addSubCategoryForm")[0].reset();
                    $("#addSubCategoryModal").modal("hide");
                } else {
                    showMessage(response.message, "danger");
                }
            },
            error: function () {
                alert("Đã xảy ra lỗi khi thêm danh mục phụ.");
            }
        });
    });

    //Chỉnh sửa danh mục con
    $(document).on("click", "#saveSubCategoryBtn", function (e) {
        e.preventDefault();
        var formData = {
            Id: $("#Id").val(),
            CategoryId: $("#CategoryId").val(),
            SubCategoryName: $("#SubCategoryName").val()
        };

        $.ajax({
            url: "/subcategory/edit-subcategory",
            type: "POST",
            contentType: "application/json",
            data: JSON.stringify(formData),
            success: function (response) {
                if (response.success) {
                    showMessage(response.message, "success");
                    setTimeout(function () {
                        window.location.href = "/subcategory/list-subcategory";
                    }, 1500);
                } else {
                    showMessage(response.message, "danger");
                }
            },
            error: function () {
                $("#message").html('<div class="alert alert-danger">Có lỗi xảy ra, vui lòng thử lại.</div>');
            }
        });
    });

    //Xóa danh mục con
    $(document).on("click", ".delete-subcategory", function (e) {
        e.preventDefault();
        var subCategoryId = $(this).data("id");
        if (!confirm("Bạn có chắc chắn muốn xóa danh mục phụ này không?")) {
            return;
        }

        $.ajax({
            url: "/subcategory/delete-subcategory",
            type: "POST",
            contentType: "application/json",
            data: JSON.stringify(subCategoryId),
            success: function (response) {
                if (response.success) {
                    $(`a.delete-subcategory[data-id="${subCategoryId}"]`).closest("tr").remove();
                    showMessage(response.message, "success");

                } else {
                    showMessage(response.message, "danger");
                }
            },
            error: function () {
                alert("Đã xảy ra lỗi khi xóa danh mục phụ.");
            }
        });
    });

    // Thêm banner
    $(document).on("click", "#saveBannerBtn", function () {
        const title = $("#bannerTitle").val().trim();
        const fileInput = $("#bannerImage")[0];
        const file = getInputPrimaryFile(fileInput);
        const status = $("#bannerStatus").val();

        // Kiểm tra dữ liệu
        if (!title) {
            showMessage("Vui lòng nhập tiêu đề!", "danger");
            return;
        }
        if (!file) {
            showMessage("Vui lòng chọn hình ảnh!", "danger");
            return;
        }

        // Tạo FormData
        const formData = new FormData();
        formData.append("Title", title);
        formData.append("ImageFile", file);
        formData.append("Status", status);

        // Gửi Ajax
        $.ajax({
            url: "/banner/add-banner",
            type: "POST",
            data: formData,
            processData: false,
            contentType: false,
            cache: false,
            beforeSend: function () {
                $("#saveBannerBtn").prop("disabled", true).text("Đang lưu...");
            },
            success: function (response) {
                if (response.success) {
                    showMessage(response.message, "success");
                    $("#addBannerModal").modal('hide'); // Đóng modal
                    setTimeout(() => location.reload(), 1000); // Reload sau 1 giây
                } else {
                    showMessage(response.message || "Lưu banner thất bại!", "danger");
                }
            },
            error: function (xhr) {
                console.error("Lỗi Ajax:", xhr.responseText);
                let errorMsg = "Lỗi Server! ";
                if (xhr.status === 400) errorMsg += "Dữ liệu không hợp lệ.";
                else if (xhr.status === 500) errorMsg += "Lỗi phía server.";
                showMessage(errorMsg, "danger");
            },
            complete: function () {
                $("#saveBannerBtn").prop("disabled", false).text("Lưu");
            }
        });
    });

    //Chỉnh sửa banner
    $(document).on("click", "#saveEditBannerBtn", function (e) {
        e.preventDefault();

        const form = $('#editBannerForm')[0];
        const formData = new FormData(form);
        appendInputFileIfAny(formData, "ImageFile", "#bannerImage");

        $.ajax({
            url: "/banner/edit-banner/" + $('#Id').val(),
            type: "POST",
            processData: false,
            contentType: false,
            data: formData,
            success: function (response) {
                console.log("Phản hồi từ server:", response);
                if (response.success) {
                    $('#message').html('<div class="alert alert-success">' + response.message + '</div>');
                    setTimeout(() => window.location.href = "/banner/list-banner", 2000);
                } else {
                    $('#message').html('<div class="alert alert-danger">' + response.message + '</div>');
                }
            },
            error: function (xhr) {
                console.log("Lỗi AJAX:", xhr.responseText);
                $('#message').html('<div class="alert alert-danger">Lỗi AJAX: ' + xhr.responseText + '</div>');
            }
        });
    });

    //Xóa banner
    $(document).on("click", ".delete-banner", function () {
        let bannerId = $(this).data("id");

        if (!bannerId || bannerId <= 0) {
            alert("ID banner không hợp lệ!");
            return;
        }

        if (confirm("Bạn có chắc muốn xóa banner này không?")) {
            $.ajax({
                url: "/banner/delete-banner",
                type: "POST",
                contentType: "application/json",
                data: JSON.stringify(bannerId),
                success: function (response) {
                    if (response.success) {
                        showMessage(response.message, "success");
                        location.reload();
                    } else {
                        showMessage(response.message, "danger");
                    }
                },
                error: function (xhr) {
                    console.log(xhr.responseText);
                    alert("Lỗi Server! Xem console để biết chi tiết.");
                }
            });
        }
    });


    //Kéo thả thứ tự banner
    $(document).on("mouseenter", "#sortable tbody", function () {
        let fixHelper = function (e, ui) {
            ui.children().each(function () {
                $(this).css("width", $(this).width()); // Chỉ set width một lần
            });
            return ui;
        };

        $(this).sortable({
            handle: ".drag-handle",
            items: "tr",
            axis: "y",
            cursor: "move",
            helper: fixHelper,
            scroll: false,
            opacity: 0.8,
            update: function () {
                let sortedIds = [];
                $("#sortable tbody tr").each(function () {
                    let id = $(this).data("id");
                    if (id) { // Kiểm tra id có tồn tại không
                        sortedIds.push(id);
                    }
                });

                console.log("Danh sách ID sau khi kéo thả: ", sortedIds);

                if (!sortedIds.length) {
                    alert("Danh sách ID không hợp lệ!");
                    return;
                }

                // Vô hiệu hóa khả năng kéo thả trong khi đang cập nhật
                let that = $(this);
                that.sortable("disable"); // Tạm thời vô hiệu hóa
                $.ajax({
                    url: "/banner/update-positions",
                    type: "POST",
                    contentType: "application/json",
                    data: JSON.stringify(sortedIds),
                    success: function (response) {
                        if (response.success) {
                            console.log("Cập nhật vị trí thành công!");
                            $("#sortable tbody tr").each(function () {
                                let id = $(this).data("id");
                                let newPosition = sortedIds.indexOf(id) + 1;
                                $(this).find(".create-date span").text(newPosition);
                            });
                        } else {
                            alert("Lỗi: " + response.message);
                        }
                        that.sortable("enable"); // Kích hoạt lại kéo thả
                    },
                    error: function (xhr, status, error) {
                        console.error("Chi tiết lỗi:", xhr.responseText);
                        alert("Lỗi cập nhật thứ tự: " + error);
                        location.reload();
                    }
                });
            }
        }).disableSelection();
    });

    //Thêm sản phẩm
    $(document).on("click", "#saveNewProductBtn", function (e) {
        e.preventDefault();
        var formData = new FormData();
        formData.append("ProductName", $("#ProductName").val());
        formData.append("CategoryId", $("#CategoryId").val());
        formData.append("SubCategoryId", $("#SubCategoryId").val());
        formData.append("ProductPrice", $("#ProductPrice").val());
        formData.append("ProductPriceBeforeDiscount", $("#ProductPriceBeforeDiscount").val());
        formData.append("Brand", $("#Brand").val());
        formData.append("CPU", $("#CPU").val());
        formData.append("RAM", $("#RAM").val());
        formData.append("Storage", $("#Storage").val());
        formData.append("GPU", $("#GPU").val());
        formData.append("VGA", $("#VGA").val());
        formData.append("Promotion", $("#Promotion").val());
        formData.append("quantity", $("#quantity").val());
        formData.append("ProductDescription", $("#editor").val());

        formData.append("PIN", $("#PIN").val());
        formData.append("WEIGHT", $("#WEIGHT").val());
        formData.append("SIZE", $("#SIZE").val());
        formData.append("BONUS", $("#BONUS").val());

        // Thêm ảnh vào formData
        appendInputFileIfAny(formData, "ProductImages", "#ProductImage1");
        appendInputFileIfAny(formData, "ProductImages", "#ProductImage2");
        appendInputFileIfAny(formData, "ProductImages", "#ProductImage3");

        $.ajax({
            url: "/product/add-product",
            type: "POST",
            data: formData,
            processData: false,
            contentType: false,
            success: function (response) {
                if (response.success) {
                    showMessage(response.message, "success");

                    // Thêm sản phẩm mới vào bảng danh sách
                    let newRow = `
                    <tr>
                        <td class="text-center">
                            <!-- Ảnh đầu tiên hiển thị -->
                            <a class="elem" href="${response.productImage1}" title="${$("#ProductName").val()} - Ảnh 1"
                               data-lcl-thumb="${response.productImage1}" rel="product-${response.productId}">
                                <img src="${response.productImage1}" alt="${$("#ProductName").val()} - Ảnh 1" class="img-thumbnail" width="100" height="100">
                            </a>
                            <!-- Ảnh ẩn để nhóm vào gallery -->
                            ${response.productImage2 ? `
                                <a class="elem hidden-images" href="${response.productImage2}" title="${$("#ProductName").val()} - Ảnh 2"
                                   data-lcl-thumb="${response.productImage2}" rel="product-${response.productId}">
                                </a>
                            ` : ''}
                            ${response.productImage3 ? `
                                <a class="elem hidden-images" href="${response.productImage3}" title="${$("#ProductName").val()} - Ảnh 3"
                                   data-lcl-thumb="${response.productImage3}" rel="product-${response.productId}">
                                </a>
                            ` : ''}
                        </td>
                        <td>${$("#ProductName").val()}</td>
                        <td>${$("#CategoryId option:selected").text()}</td>
                        <td>${$("#quantity").val()}</td>
                        <td>${parseInt($("#ProductPrice").val()).toLocaleString('vi-VN')} VNĐ</td>
                        <td>${$("#ProductPriceBeforeDiscount").val() ? parseInt($("#ProductPriceBeforeDiscount").val()).toLocaleString('vi-VN') + ' VNĐ' : 'Không có'}</td>
                        <td class="action">
                            <a href="/product/edit-product/${response.productId}">
                                <i class="ri-pencil-line"></i>
                            </a>
                           <a href="javascript:void(0);" class="text-danger delete-product" data-id="${response.productId}">
                                <i class="ri-delete-bin-6-line"></i>
                           </a>
                        </td>
                    </tr>
                `;
                    $("#productTable tbody").prepend(newRow); // Thêm vào đầu danh sách

                    // Reset form và đóng modal
                    $("#addProductForm")[0].reset();
                    $("#addProductModal").modal("hide");

                    // Khởi tạo lại lc-lightbox cho các ảnh mới (nếu cần)
                    if (typeof lc_lightbox !== 'undefined') {
                        lc_lightbox('.elem');
                    }
                } else {
                    showMessage(response.message, "danger");
                }
            },
            error: function () {
                alert("Có lỗi xảy ra!");
            }
        });
    });

    // Chỉnh sửa sản phẩm
    $(document).on("click", "#saveEditProductBtn", function (e) {
        e.preventDefault();

        var productId = $("#Id").val(); // Lấy productId từ input ẩn
        var formData = new FormData();
        formData.append("ProductName", $("#ProductName").val());
        formData.append("CategoryId", $("#editCategoryId").val());
        formData.append("SubCategoryId", $("#editSubCategoryId").val());
        formData.append("ProductPrice", $("#ProductPrice").val());
        formData.append("ProductPriceBeforeDiscount", $("#ProductPriceBeforeDiscount").val());
        formData.append("Brand", $("#Brand").val());
        formData.append("CPU", $("#CPU").val());
        formData.append("RAM", $("#RAM").val());
        formData.append("Storage", $("#Storage").val());
        formData.append("GPU", $("#GPU").val());
        formData.append("VGA", $("#VGA").val());
        formData.append("Promotion", $("#Promotion").val());
        formData.append("ProductDescription", $("#editor").val());
        formData.append("quantity", $("#quantity").val());

        formData.append("PIN", $("#PIN").val());
        formData.append("WEIGHT", $("#WEIGHT").val());
        formData.append("SIZE", $("#SIZE").val());
        formData.append("BONUS", $("#BONUS").val());

        // Thêm ảnh vào formData
        appendInputFileIfAny(formData, "ProductImages", "#editProductImage1");
        appendInputFileIfAny(formData, "ProductImages", "#editProductImage2");
        appendInputFileIfAny(formData, "ProductImages", "#editProductImage3");

        $.ajax({
            url: `/product/edit-product/${productId}`,
            type: "POST",
            data: formData,
            processData: false,
            contentType: false,
            success: function (response) {
                if (response.success) {
                    showMessage(response.message, "success");

                    // Cập nhật hàng trong bảng danh sách sản phẩm
                    let updatedRow = `
                    <tr>
                        <td class="text-center">
                            <a href="${response.productImage1 || ''}" class="glightbox" data-gallery="gallery-${response.productId}">
                                <img src="${response.productImage1 || ''}" alt="Ảnh 1" class="img-thumbnail" width="150">
                            </a>
                            <a href="${response.productImage2 || ''}" class="glightbox" data-gallery="gallery-${response.productId}" style="display: none;"></a>
                            <a href="${response.productImage3 || ''}" class="glightbox" data-gallery="gallery-${response.productId}" style="display: none;"></a>
                        </td>
                        <td>${$("#ProductName").val()}</td>
                        <td>${$("#editCategoryId option:selected").text()}</td>
                        <td>${$("#quantity").val()}</td>
                        <td>${$("#Promotion").val() || 'Không có'}</td>
                        <td>${parseInt($("#ProductPrice").val()).toLocaleString('vi-VN')} VNĐ</td>
                        <td>${$("#ProductPriceBeforeDiscount").val() ? parseInt($("#ProductPriceBeforeDiscount").val()).toLocaleString('vi-VN') + ' VNĐ' : 'Không có'}</td>
                        <td class="text-center">
                            <a href="/product/edit-product/${response.productId}" class="btn btn-warning btn-sm">
                                <i class="ri-pencil-line"></i>
                            </a>
                            <button class="btn btn-danger btn-sm delete-product" data-id="${response.productId}">
                                <i class="ri-delete-bin-6-line"></i>
                            </button>
                        </td>
                    </tr>
                `;

                    // Tìm và thay thế hàng cũ bằng hàng mới
                    $(`#productTable tbody tr:contains(${response.productId})`).replaceWith(updatedRow);

                    // Chuyển hướng về trang danh sách

                    setTimeout(() => {
                        window.location.href = "/product/list-product";
                    }, 2000);
                } else {
                    showMessage(response.message, "danger");
                }
            },
            error: function (xhr) {
                console.log(xhr); 

                let msg = "Có lỗi xảy ra!";

                if (xhr.responseJSON?.message) {
                    msg = xhr.responseJSON.message;
                } else if (xhr.responseText) {
                    msg = xhr.responseText;
                }

                showMessage(msg, "danger");
            }
        });
    });

    //Xóa sản phẩm
    $(document).on("click", ".delete-product", function (e) {
        e.preventDefault(); // Ngăn hành vi mặc định của thẻ <a>
        let productId = $(this).data("id");

        // Hiển thị hộp thoại xác nhận
        if (!confirm("Bạn có chắc chắn muốn xóa sản phẩm này?")) return;

        $.ajax({
            url: "/product/delete-product", 
            type: "POST",
            data: { productId: productId }, 
            headers: {
                'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val() 
            },
            success: function (response) {
                // Giả sử bạn có hàm showMessage để hiển thị thông báo
                showMessage(response.message, response.success ? "success" : "danger");

                if (response.success) {
                    // Xóa hàng chứa sản phẩm đã bị xóa
                    $("tr").filter(function () {
                        return $(this).find(".delete-product").data("id") == productId;
                    }).remove();
                }
            },
            error: function (xhr, status, error) {
                showMessage("Đã xảy ra lỗi: " + error, "danger");
            }
        });
    });

    //Hàm load danh sách sản phẩm theo danh mục chính cho cả hai
    $(document).on("change", "#CategoryId, #editCategoryId", function () {
        let subCategorySelector = $(this).attr("id") === "CategoryId" ? "#SubCategoryId" : "#editSubCategoryId";
        loadSubCategories(this, subCategorySelector);
    });

    // Hàm lấy số lượng đơn hàng chưa hủy trong 3 ngày gần nhất và cập nhật badge
    function updateOrderCountBadge() {
        $.ajax({
            url: "/admin/order/get-new-orders-count",
            type: "GET",
            dataType: "json",
            xhrFields: {
                withCredentials: true // Đảm bảo gửi cookie xác thực
            },
            success: function (response) {
                console.log("Phản hồi từ server (badge):", response);
                if (response.success) {
                    $("#orderCountBadge").html(`${response.count}<span class="visually-hidden">unread messages</span>`);
                    // Ẩn badge nếu không có đơn hàng
                    if (response.count === 0) {
                        $("#orderCountBadge").hide();
                    } else {
                        $("#orderCountBadge").show();
                    }
                } else {
                    console.log("Lỗi khi lấy số lượng đơn hàng cho badge:", response.message);
                    $("#orderCountBadge").html(`0<span class="visually-hidden">unread messages</span>`);
                    $("#orderCountBadge").hide();
                }
            },
            error: function (xhr) {
                console.log("Lỗi AJAX (badge):", xhr.responseText);
                $("#orderCountBadge").html(`0<span class="visually-hidden">unread messages</span>`);
                $("#orderCountBadge").hide();
            }
        });
    }

    // Gọi hàm cập nhật badge khi trang được tải
    $(function () {
        updateOrderCountBadge();
    });


    // Xử lý khi nhấn vào "Thông tin đơn hàng"
    $(document).on("click", "#newOrdersLink", function (e) {
        e.preventDefault();
            clicked = true;

        $.ajax({
            url: "/admin/order/get-new-orders-count",
            type: "GET",
            dataType: "json",
            xhrFields: {
                withCredentials: true // Đảm bảo gửi cookie xác thực
            },
            success: function (response) {
                console.log("Phản hồi từ server:", response);
                if (response.success) {
                    $("#newOrdersCount").text(`Có ${response.count} đơn hàng mới trong 3 ngày gần nhất.`);
                    $("#newOrdersInfo").show();
                } else {
                    $("#newOrdersCount").text("Không thể lấy thông tin đơn hàng: " + response.message);
                    $("#newOrdersInfo").show();
                }
            },
            error: function (xhr) {
                console.log("Lỗi AJAX:", xhr.responseText);
                $("#newOrdersCount").text("Lỗi khi lấy thông tin đơn hàng.");
                $("#newOrdersInfo").show();
            }
        });
    });

    //Xử lý trạng thái đơn hàng
    $(document).on("change", ".update-status", function () {
        var orderId = $(this).data("order-id");
        var newStatus = $(this).val();

        $.ajax({
            url: "/admin/order/update-order-status",
            type: "POST",
            data: { orderId: orderId, status: newStatus },
            success: function (response) {
                console.log("Phản hồi từ server:", response);
                if (response.success) {
                    $("#message").html('<div class="alert alert-success">' + response.message + '</div>');
                    // Cập nhật màu sắc của badge trạng thái
                    var badge = $(`select[data-order-id='${orderId}']`).closest("tr").find(".badge");
                    badge.removeClass("bg-warning bg-info bg-success bg-danger");
                    if (newStatus === "Pending") {
                        badge.addClass("bg-warning");
                    } else if (newStatus === "Paid") {
                        badge.addClass("bg-info");
                    } else if (newStatus === "Shipping") {
                        badge.addClass("bg-info");
                    }else if (newStatus === "Delivered") {
                        badge.addClass("bg-success");
                    } else if (newStatus === "Cancelled") {
                        badge.addClass("bg-danger");
                    }
                    badge.text(newStatus);
                } else {
                    $("#message").html('<div class="alert alert-danger">' + response.message + '</div>');
                }
            },
            error: function (xhr) {
                console.log("Lỗi AJAX:", xhr.responseText);
                $("#message").html('<div class="alert alert-danger">Lỗi khi cập nhật trạng thái đơn hàng.</div>');
            }
        });
    });
});



// =========================================================================
// SỰ KIỆN USER
// =========================================================================
$(function () {
    let verifyCooldownTimer = null;
    let forgotCooldownTimer = null;
    const commonEmailTypos = {
        'gmal.com': 'gmail.com',
        'gmial.com': 'gmail.com',
        'gmail.con': 'gmail.com',
        'gmail.co': 'gmail.com',
        'hotmail.co': 'hotmail.com',
        'hotnail.com': 'hotmail.com',
        'yaho.com': 'yahoo.com',
        'yahooo.com': 'yahoo.com',
        'outlok.com': 'outlook.com',
        'outllok.com': 'outlook.com'
    };

    function validateEmailWithSuggestion(email) {
        const value = (email || '').trim().toLowerCase();
        const simpleRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

        if (!value || !simpleRegex.test(value)) {
            return { valid: false, message: 'Email không hợp lệ.' };
        }

        const parts = value.split('@');
        if (parts.length !== 2) {
            return { valid: false, message: 'Email không hợp lệ.' };
        }

        const local = parts[0];
        const domain = parts[1];
        if (commonEmailTypos[domain]) {
            return {
                valid: false,
                message: `Bạn có nhập sai email không? Gợi ý: ${local}@${commonEmailTypos[domain]}`
            };
        }

        return { valid: true, message: '' };
    }

    function isStrongPassword(password) {
        const value = password || '';
        return value.length >= 8 && /[A-Z]/.test(value) && /[a-z]/.test(value) && /\d/.test(value);
    }

    function updateResetPasswordChecklist(password) {
        const value = password || '';
        const checks = [
            { selector: '#rule-length', valid: value.length >= 8 },
            { selector: '#rule-upper', valid: /[A-Z]/.test(value) },
            { selector: '#rule-lower', valid: /[a-z]/.test(value) },
            { selector: '#rule-digit', valid: /\d/.test(value) }
        ];

        checks.forEach(function (check) {
            const $item = $(check.selector);
            if ($item.length === 0) {
                return;
            }

            $item.toggleClass('valid', check.valid);
            $item.toggleClass('invalid', !check.valid);
        });
    }

    function updateSignupPasswordChecklist(password) {
        const value = password || '';
        const checks = [
            { selector: '#signup-rule-length', valid: value.length >= 8 },
            { selector: '#signup-rule-upper', valid: /[A-Z]/.test(value) },
            { selector: '#signup-rule-lower', valid: /[a-z]/.test(value) },
            { selector: '#signup-rule-digit', valid: /\d/.test(value) }
        ];

        checks.forEach(function (check) {
            const $item = $(check.selector);
            if ($item.length === 0) {
                return;
            }

            $item.toggleClass('valid', check.valid);
            $item.toggleClass('invalid', !check.valid);
        });
    }

    function setOtpHint(selector, message) {
        const $hint = $(selector);
        if ($hint.length > 0) {
            $hint.text(message || '');
        }
    }

    function startOtpCooldown($button, seconds, idleText, timerRefName, hintSelector, doneHintText) {
        if (!seconds || seconds <= 0) {
            return;
        }

        const hasButton = !!($button && $button.length > 0);

        if (timerRefName === 'verify' && verifyCooldownTimer) {
            clearInterval(verifyCooldownTimer);
            verifyCooldownTimer = null;
        }

        if (timerRefName === 'forgot' && forgotCooldownTimer) {
            clearInterval(forgotCooldownTimer);
            forgotCooldownTimer = null;
        }

        let remaining = seconds;
        if (hasButton) {
            $button.prop('disabled', true).text(`Gửi lại sau ${remaining}s`);
        }
        if (hintSelector) {
            setOtpHint(hintSelector, `Bạn có thể yêu cầu gửi lại OTP sau ${remaining}s.`);
        }

        const timer = setInterval(function () {
            remaining -= 1;
            if (remaining <= 0) {
                clearInterval(timer);
                if (hasButton) {
                    $button.prop('disabled', false).text(idleText);
                }
                if (hintSelector) {
                    setOtpHint(hintSelector, doneHintText || 'Bạn có thể yêu cầu gửi lại OTP ngay bây giờ.');
                }
                if (timerRefName === 'verify') {
                    verifyCooldownTimer = null;
                } else if (timerRefName === 'forgot') {
                    forgotCooldownTimer = null;
                }
                return;
            }

            if (hasButton) {
                $button.text(`Gửi lại sau ${remaining}s`);
            }
            if (hintSelector) {
                setOtpHint(hintSelector, `Bạn có thể yêu cầu gửi lại OTP sau ${remaining}s.`);
            }
        }, 1000);

        if (timerRefName === 'verify') {
            verifyCooldownTimer = timer;
        } else if (timerRefName === 'forgot') {
            forgotCooldownTimer = timer;
        }
    }

    function openVerifyEmailForm(email) {
        $("#VerifyEmail").val(email || "");
        $("#VerifyOtp").val("");
        setOtpHint('#verify-otp-hint', 'OTP có hiệu lực trong 1 phút.');
        $("#login-form, #signup-form, #forgot-password-form").hide();
        $("#verify-email-form").show();
    }

    function openForgotPasswordForm() {
        setOtpHint('#forgot-otp-hint', 'OTP sẽ được gửi trực tiếp về email đang nhập ở form đăng nhập.');
        setOtpHint('#reset-otp-hint', 'OTP đặt lại mật khẩu có hiệu lực trong 1 phút.');
        const loginEmail = $('#Email').val()?.trim() || '';
        $('#ResetEmail').val(loginEmail);
        $("#login-form, #signup-form, #verify-email-form").hide();
        $("#forgot-password-form").show();
        $("#resetPasswordForm").hide();
        $("#reset-password-step").hide();
        $("#forgotPasswordRequestForm").show();
    }

    function resolveForgotEmail() {
        const hiddenEmail = $('#ResetEmail').val()?.trim() || '';
        const loginEmail = $('#Email').val()?.trim() || '';
        const email = hiddenEmail || loginEmail;
        if (email) {
            $('#ResetEmail').val(email);
        }
        return email;
    }

    // Đăng nhập User
    $(document).on("submit", "#loginForm", function (e) {
        e.preventDefault();
        const emailuser = $("#Email").val()?.trim() || "";
        const passworduser = $("#Password").val()?.trim() || "";
        const rememberMe = $("#RememberMe")?.is(":checked") || false; // Thêm RememberMe nếu có

        if (!emailuser) return showMessage("Vui lòng nhập email!", "danger", '#login-message');
        const loginEmailCheck = validateEmailWithSuggestion(emailuser);
        if (!loginEmailCheck.valid) return showMessage(loginEmailCheck.message, "danger", '#login-message');
        if (!passworduser) return showMessage("Vui lòng nhập mật khẩu!", "danger", '#login-message');

        $.ajax({
            url: "/UserAuth/Login" + (window.location.search || ""),
            type: "POST",
            contentType: "application/json",
            data: JSON.stringify({ Email: emailuser, Password: passworduser, RememberMe: rememberMe }),
            success: function (response) {
                console.log("Response:", response);
                if (response.success) {
                    showMessage(response.message, "success", '#login-message');
                    setTimeout(function () {
                        window.location.href = response.redirectUrl || "/";
                    }, 2000);
                } else if (response.requiresVerification && response.email) {
                    showMessage(response.message, "warning", '#login-message');
                    openVerifyEmailForm(response.email);
                } else {
                    showMessage(response.message, "danger", '#login-message');
                }
            },
            error: function (xhr) {
                console.error("AJAX Error:", xhr.responseText);
                showMessage("Đã xảy ra lỗi! Vui lòng thử lại.", "danger", '#login-message');
            }
        });
    });

    //Đăng ký User
    $(document).on('submit', '#signupForm', function (e) {
        e.preventDefault();

        const email = $('#SignupEmail').val()?.trim() || '';
        const username = $('#Username').val()?.trim() || '';
        const phone = $('#Phone').val()?.trim() || '';
        const password = $('#SignupPassword').val()?.trim() || '';
        const confirmPassword = $('#SignupConfirmPassword').val()?.trim() || '';
        if (!email) return showMessage('Vui lòng nhập email!', 'danger', '#signup-message');
        const signupEmailCheck = validateEmailWithSuggestion(email);
        if (!signupEmailCheck.valid) return showMessage(signupEmailCheck.message, 'danger', '#signup-message');
        if (!username) return showMessage('Vui lòng nhập họ tên!', 'danger', '#signup-message');
        if (!phone) return showMessage('Vui lòng nhập số điện thoại!', 'danger', '#signup-message');
        if (!password) return showMessage('Vui lòng nhập mật khẩu!', 'danger', '#signup-message');
        if (!isStrongPassword(password)) return showMessage('Mật khẩu phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường và số.', 'danger', '#signup-message');
        if (!confirmPassword) return showMessage('Vui lòng nhập xác nhận mật khẩu!', 'danger', '#signup-message');
        if (password !== confirmPassword) return showMessage('Mật khẩu xác nhận không khớp!', 'danger', '#signup-message');

        $.ajax({
            url: '/UserAuth/Register',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                Email: email,
                Username: username,
                Phone: phone,
                Password: password,
                Password2: confirmPassword
            }),
            success: function (response) {
                if (response.success) {
                    showMessage(response.message, 'success', '#signup-message');
                    if (response.requiresVerification && response.email) {
                        openVerifyEmailForm(response.email);
                        startOtpCooldown(
                            $('#resend-verify-otp-btn'),
                            response.cooldownSeconds || 60,
                            'Gửi lại OTP',
                            'verify',
                            '#verify-otp-hint',
                            'OTP có hiệu lực trong 1 phút. Bạn có thể bấm gửi lại nếu chưa nhận được email.'
                        );
                    }
                } else {
                    showMessage(response.message, 'danger', '#signup-message');
                }
            },
            error: function (xhr) {
                console.error('AJAX Error:', xhr.responseText);
                showMessage('Đã xảy ra lỗi! Vui lòng thử lại.', 'danger', '#signup-message');
            }
        });
    });

    $(document).on('click', '.toggle-password-btn', function () {
        const selector = $(this).data('target');
        const $target = $(selector);
        if ($target.length === 0) {
            return;
        }

        const isPassword = $target.attr('type') === 'password';
        const $icon = $(this).find('i');
        $target.attr('type', isPassword ? 'text' : 'password');
        $(this).toggleClass('active', isPassword);
        $(this).attr('title', isPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu');
        $(this).attr('aria-label', isPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu');

        if ($icon.length > 0) {
            if (isPassword) {
                $icon.removeClass('ri-eye-line').addClass('ri-eye-off-line');
            } else {
                $icon.removeClass('ri-eye-off-line').addClass('ri-eye-line');
            }
        }
    });

    $(document).on('submit', '#verifyEmailForm', function (e) {
        e.preventDefault();

        const email = $('#VerifyEmail').val()?.trim() || '';
        const otp = $('#VerifyOtp').val()?.trim() || '';

        if (!email) return showMessage('Thiếu email xác thực.', 'danger', '#verify-message');
        if (!otp || otp.length !== 6) return showMessage('Vui lòng nhập OTP 6 số.', 'danger', '#verify-message');

        $.ajax({
            url: '/UserAuth/VerifyEmailOtp',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ Email: email, Otp: otp }),
            success: function (response) {
                if (response.success) {
                    showMessage(response.message, 'success', '#verify-message');
                    setTimeout(function () {
                        window.location.href = response.redirectUrl || '/';
                    }, 1500);
                } else {
                    showMessage(response.message, 'danger', '#verify-message');
                }
            },
            error: function () {
                showMessage('Đã xảy ra lỗi! Vui lòng thử lại.', 'danger', '#verify-message');
            }
        });
    });

    $(document).on('click', '#resend-verify-otp-btn', function () {
        const email = $('#VerifyEmail').val()?.trim() || '';
        if (!email) return showMessage('Thiếu email để gửi OTP.', 'danger', '#verify-message');

        $.ajax({
            url: '/UserAuth/ResendVerificationOtp',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ Email: email }),
            success: function (response) {
                showMessage(response.message, response.success ? 'success' : 'danger', '#verify-message');
                if (response.cooldownSeconds) {
                    startOtpCooldown(
                        $('#resend-verify-otp-btn'),
                        response.cooldownSeconds,
                        'Gửi lại OTP',
                        'verify',
                        '#verify-otp-hint',
                        'OTP có hiệu lực trong 1 phút. Bạn có thể bấm gửi lại nếu chưa nhận được email.'
                    );
                }
            },
            error: function () {
                showMessage('Không thể gửi lại OTP.', 'danger', '#verify-message');
            }
        });
    });

    function sendForgotPasswordOtp() {
        const email = resolveForgotEmail();
        if (!email) {
            showMessage('Vui lòng nhập email ở form đăng nhập trước.', 'danger', '#forgot-message');
            return;
        }

        const forgotEmailCheck = validateEmailWithSuggestion(email);
        if (!forgotEmailCheck.valid) {
            showMessage(forgotEmailCheck.message, 'danger', '#forgot-message');
            return;
        }

        showMessage('Đang gửi OTP về email của bạn...', 'info', '#forgot-message');

        $.ajax({
            url: '/UserAuth/ForgotPasswordRequest',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ Email: email }),
            success: function (response) {
                let forgotMessage = response.message || '';
                if (response.success && response.otpPreview) {
                    forgotMessage += ` OTP test (dev): ${response.otpPreview}`;
                }
                showMessage(forgotMessage, response.success ? 'success' : 'danger', '#forgot-message');
                if (response.cooldownSeconds) {
                    startOtpCooldown(
                        $('#resend-forgot-otp-btn'),
                        response.cooldownSeconds,
                        'Gửi lại OTP',
                        'forgot',
                        '#forgot-otp-hint',
                        'OTP có hiệu lực trong 1 phút. Nếu chưa nhận được email, bạn có thể bấm "Gửi lại OTP".'
                    );
                }
                if (response.success) {
                    $('#ResetEmail').val(email);
                    $('#ResetOtp').val('');
                    $('#resetPasswordForm').hide();
                    $('#reset-password-step').hide();
                    $('#ResetOtp').trigger('focus');
                }
            },
            error: function () {
                showMessage('Không thể gửi OTP đặt lại mật khẩu.', 'danger', '#forgot-message');
            }
        });
    }

    $(document).on('click', '#forgot-password-link', function () {
        openForgotPasswordForm();
        sendForgotPasswordOtp();
    });

    $(document).on('click', '#resend-forgot-otp-btn', function () {
        sendForgotPasswordOtp();
    });

    $(document).on('keydown', '#ResetOtp', function (e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            $('#verify-reset-otp-btn').trigger('click');
        }
    });

    $(document).on('click', '#verify-reset-otp-btn', function () {
        const $verifyButton = $('#verify-reset-otp-btn');
        const email = resolveForgotEmail();
        const otp = $('#ResetOtp').val()?.trim() || '';

        if (!email) return showMessage('Thiếu email đặt lại mật khẩu.', 'danger', '#forgot-message');
        if (!otp || otp.length !== 6) return showMessage('Vui lòng nhập OTP 6 số.', 'danger', '#forgot-message');

        $verifyButton.prop('disabled', true).text('Đang xác nhận...');

        $.ajax({
            url: '/UserAuth/VerifyForgotPasswordOtp',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ Email: email, Otp: otp }),
            success: function (response) {
                if (response.success) {
                    showMessage(response.message, 'success', '#forgot-message');
                    const redirectUrl = response.redirectUrl || `/UserAuth/ResetPassword?email=${encodeURIComponent(email)}&otp=${encodeURIComponent(otp)}`;
                    setTimeout(function () {
                        window.location.href = redirectUrl;
                    }, 120);
                } else {
                    showMessage(response.message, 'danger', '#forgot-message');
                }
            },
            error: function () {
                showMessage('Không thể xác nhận OTP.', 'danger', '#forgot-message');
            },
            complete: function () {
                $verifyButton.prop('disabled', false).text('Xác nhận OTP');
            }
        });
    });

    $(document).on('submit', '#resetPasswordPageForm', function (e) {
        e.preventDefault();

        const email = $('#ResetPageEmail').val()?.trim() || '';
        const otp = $('#ResetPageOtp').val()?.trim() || '';
        const newPassword = $('#ResetPageNewPassword').val()?.trim() || '';
        const confirmPassword = $('#ResetPageConfirmPassword').val()?.trim() || '';

        if (!email) return showMessage('Thiếu email đặt lại mật khẩu.', 'danger', '#reset-page-message');
        const resetPageEmailCheck = validateEmailWithSuggestion(email);
        if (!resetPageEmailCheck.valid) return showMessage(resetPageEmailCheck.message, 'danger', '#reset-page-message');
        if (!otp || otp.length !== 6) return showMessage('OTP không hợp lệ.', 'danger', '#reset-page-message');
        if (!isStrongPassword(newPassword)) return showMessage('Mật khẩu mới phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường và số.', 'danger', '#reset-page-message');
        if (newPassword !== confirmPassword) return showMessage('Mật khẩu xác nhận không khớp.', 'danger', '#reset-page-message');

        $.ajax({
            url: '/UserAuth/ResetPasswordWithOtp',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                Email: email,
                Otp: otp,
                NewPassword: newPassword,
                ConfirmPassword: confirmPassword
            }),
            success: function (response) {
                if (response.success) {
                    showMessage(response.message, 'success', '#reset-page-message');
                    setTimeout(function () {
                        window.location.href = '/UserAuth/Login';
                    }, 1200);
                } else {
                    showMessage(response.message, 'danger', '#reset-page-message');
                }
            },
            error: function () {
                showMessage('Không thể đặt lại mật khẩu.', 'danger', '#reset-page-message');
            }
        });
    });

    $(document).on('input', '#ResetPageNewPassword', function () {
        updateResetPasswordChecklist($(this).val());
    });

    $(document).on('input', '#SignupPassword', function () {
        updateSignupPasswordChecklist($(this).val());
    });

    if ($('#ResetPageNewPassword').length > 0) {
        updateResetPasswordChecklist($('#ResetPageNewPassword').val());
    }

    if ($('#SignupPassword').length > 0) {
        updateSignupPasswordChecklist($('#SignupPassword').val());
    }

    $(document).on('submit', '#resetPasswordForm', function (e) {
        e.preventDefault();

        const email = resolveForgotEmail();
        const otp = $('#ResetOtp').val()?.trim() || '';
        const newPassword = $('#ResetNewPassword').val()?.trim() || '';
        const confirmPassword = $('#ResetConfirmPassword').val()?.trim() || '';

        if ($('#reset-password-step').is(':hidden')) {
            return showMessage('Vui lòng xác nhận OTP trước khi đổi mật khẩu.', 'danger', '#reset-message');
        }

        if (!email) return showMessage('Thiếu email đặt lại mật khẩu.', 'danger', '#reset-message');
        const resetEmailCheck = validateEmailWithSuggestion(email);
        if (!resetEmailCheck.valid) return showMessage(resetEmailCheck.message, 'danger', '#reset-message');
        if (!otp || otp.length !== 6) return showMessage('Vui lòng nhập OTP 6 số.', 'danger', '#reset-message');
        if (!isStrongPassword(newPassword)) return showMessage('Mật khẩu mới phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường và số.', 'danger', '#reset-message');
        if (newPassword !== confirmPassword) return showMessage('Mật khẩu xác nhận không khớp.', 'danger', '#reset-message');

        $.ajax({
            url: '/UserAuth/ResetPasswordWithOtp',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                Email: email,
                Otp: otp,
                NewPassword: newPassword,
                ConfirmPassword: confirmPassword
            }),
            success: function (response) {
                if (response.success) {
                    showMessage(response.message, 'success', '#reset-message');
                    setTimeout(function () {
                        toggleLogin();
                        $('#forgot-password-form').hide();
                        $('#login-form').show();
                    }, 1200);
                } else {
                    showMessage(response.message, 'danger', '#reset-message');
                }
            },
            error: function () {
                showMessage('Không thể đặt lại mật khẩu.', 'danger', '#reset-message');
            }
        });
    });

    $(document).on('click', '#back-to-login-from-verify, #back-to-login-from-forgot', function () {
        $('#verify-email-form, #forgot-password-form').hide();
        toggleLogin();
    });

    //Đánh giá sản phẩm
    document.querySelectorAll('.star-rating i').forEach(star => {
        star.addEventListener('click', function () {
            const value = this.getAttribute('data-value');
            document.getElementById('rating').value = value;

            // Cập nhật giao diện ngôi sao
            document.querySelectorAll('.star-rating i').forEach(s => {
                if (s.getAttribute('data-value') <= value) {
                    s.classList.remove('far');
                    s.classList.add('fas');
                } else {
                    s.classList.remove('fas');
                    s.classList.add('far');
                }
            });
        });
    });

    // Xử lý tăng/giảm số lượng trong giỏ hàng
    $(document).on('click', '.custom-quantity-btn', function () {
        var container = $(this).closest('.custom-quantity-group');
        var input = container.find('.custom-quantity-input').first();
        var quantity = parseInt(input.val()) || 0;
        var cartItemId = $(this).data('id');

        if ($(this).hasClass('btn-plus')) {
            quantity++;
        } else if ($(this).hasClass('btn-minus') && quantity > 1) {
            quantity--;
        }

        // Cập nhật số lượng trên giao diện trước
        input.val(quantity).trigger('change');

        // Cập nhật tổng giá (bao gồm cả tóm tắt giỏ hàng)
        updateTotalPrice();

        // Cập nhật số lượng giỏ hàng (gọi updateCartCount nếu cần)
        updateCartCount();

        // Gửi AJAX cập nhật số lượng giỏ hàng
        updateCartItemQuantity(cartItemId, quantity);
    });

    // Gửi AJAX cập nhật số lượng giỏ hàng
    function updateCartItemQuantity(cartItemId, quantity) {
        console.log('[DEBUG] Gửi UpdateQuantity - ID:', cartItemId, 'Quantity:', quantity);

        if (!cartItemId) {
            console.error('[DEBUG] cartItemId không hợp lệ!');
            return;
        }

        $.ajax({
            url: '/Cart/UpdateQuantity',
            type: 'POST',
            data: { cartItemId: cartItemId, quantity: quantity },
            success: function (response) {
                if (response.success) {
                    console.log('[DEBUG] Cập nhật thành công:', response);
                    updateCartCount();
                    updateTotalPrice(); // Cập nhật tổng giá sau khi AJAX thành công
                } else {
                    alert(response.message);
                }
            },
            error: function (xhr) {
                console.error('[DEBUG] Lỗi AJAX:', xhr.status, xhr.statusText);
            }
        });
    }

    // Cập nhật tổng giá sau khi thay đổi số lượng
    function updateTotalPrice() {
        // Cập nhật tổng giá cho từng hàng
        $('.custom-quantity-group').each(function () {
            var price = parseFloat($(this).data('price')) || 0;
            var quantity = parseInt($(this).find('.custom-quantity-input').val()) || 0;
            var rowTotal = price * quantity;

            var row = $(this).closest('tr');
            row.find('.custom-total').text(rowTotal.toLocaleString() + ' VNĐ');
        });

        // Tính toán cho phần "Tóm tắt giỏ hàng"
        let subtotal = 0; // Tạm tính
        let shipping = 0; // Phí vận chuyển
        $('.custom-quantity-group').each(function () {
            var price = parseFloat($(this).data('price')) || 0;
            var quantity = parseInt($(this).find('.custom-quantity-input').val()) || 0;
            var shippingCharge = parseFloat($(this).data('shipping-charge')) || 0;

            subtotal += price * quantity;
            shipping += shippingCharge; // Phí vận chuyển không phụ thuộc vào số lượng
        });

        let grandTotal = subtotal + shipping; // Tổng cộng

        // Cập nhật phần "Tóm tắt giỏ hàng"
        $('#subtotal').text(subtotal.toLocaleString() + ' VNĐ');
        $('#shipping').text(shipping.toLocaleString() + ' VNĐ');
        $('#grand-total').text(grandTotal.toLocaleString() + ' VNĐ');
    }

    //Thêm vào giỏ hàng
    $(document).on('click', '#add-to-cart-btn', function (e) {
        e.preventDefault();

        var productId = $(this).data('product-id');
        var quantity = parseInt($('#quantity').val()) || 1;
        var cartIcon = $('.icon-cart'); // Icon giỏ hàng
        var addToCartBtn = $(this); // Nút "Thêm vào giỏ hàng"

        // Lấy ảnh sản phẩm gần nút "Thêm vào giỏ hàng" nhất
        var productImg = addToCartBtn.closest('.product-item').find('img').first();
        if (productImg.length) {
            var flyingImg = productImg.clone().css({
                position: 'absolute',
                width: productImg.width(),
                height: productImg.height(),
                zIndex: 1000,
                top: productImg.offset().top,
                left: productImg.offset().left,
                opacity: 1
            }).appendTo('body');

            flyingImg.animate({
                top: cartIcon.offset().top + 10,
                left: cartIcon.offset().left + 10,
                width: 50,
                height: 50,
                opacity: 0
            }, 800, 'easeInOutQuad', function () {
                $(this).remove();

                // Hiệu ứng rung icon giỏ hàng
                cartIcon.addClass('shake');
                setTimeout(() => cartIcon.removeClass('shake'), 500);
            });
        }

        // Gửi yêu cầu AJAX thêm sản phẩm vào giỏ hàng
        $.ajax({
            url: '/Cart/AddToCart',
            type: 'POST',
            data: { productId: productId, quantity: quantity },
            xhrFields: { withCredentials: true },
            success: function (response) {
                if (response.success) {
                    updateCartCount();
                  
                } else {
                    showMessage(response.message, 'danger');
                }
            },
            error: function () {
                showMessage('Đã xảy ra lỗi khi thêm vào giỏ hàng!', 'danger');
            }
        });
    });

    // Mua ngay: thêm sản phẩm vào giỏ rồi chuyển thẳng đến checkout
    $(document).on('click', '.btn-buy-now', function (e) {
        e.preventDefault();

        var productId = $(this).data('product-id');
        var quantity = parseInt($('#quantity').val()) || 1;

        $.ajax({
            url: '/Cart/AddToCart',
            type: 'POST',
            data: { productId: productId, quantity: quantity },
            xhrFields: { withCredentials: true },
            success: function (response) {
                if (response.success) {
                    window.location.href = '/Cart/Checkout';
                } else {
                    showMessage(response.message, 'danger');
                }
            },
            error: function () {
                showMessage('Đã xảy ra lỗi khi xử lý mua ngay!', 'danger');
            }
        });
    });

    //Thêm vào giỏ hàng
    $(document).on('click', '#add-to-fav-btn', function (e) {
        e.preventDefault();
        console.log("Superman");
        var productId = $(this).data('product-id');
        var favIcon = $('.icon-fav'); // Icon giỏ hàng
        var addTofavBtn = $(this); // Nút "Thêm vào giỏ hàng"

        // Lấy ảnh sản phẩm gần nút "Thêm vào giỏ hàng" nhất
        var productImg = addTofavBtn.closest('.product-item').find('img').first();
        if (productImg.length) {
            var flyingImg = productImg.clone().css({
                position: 'absolute',
                width: productImg.width(),
                height: productImg.height(),
                zIndex: 1000,
                top: productImg.offset().top,
                left: productImg.offset().left,
                opacity: 1
            }).appendTo('body');

            flyingImg.animate({
                top: favIcon.offset().top + 10,
                left: favIcon.offset().left + 10,
                width: 50,
                height: 50,
                opacity: 0
            }, 800, 'easeInOutQuad', function () {
                $(this).remove();

                // Hiệu ứng rung icon giỏ hàng
                favIcon.addClass('shake');
                setTimeout(() => favIcon.removeClass('shake'), 500);
            });
        }

        // Gửi yêu cầu AJAX thêm sản phẩm vào giỏ hàng
        $.ajax({
            url: '/Fav/AddToFav',
            type: 'POST',
            data: { productId: productId },
            xhrFields: { withCredentials: true },
            success: function (response) {
                if (response.success) {
                    updateFavCount();

                } else {
                    alert(response.message);
                }
            },
            error: function () {
                alert('Đã xảy ra lỗi khi thêm vào ưa thích!');
            }
        });
    });

    //Hủy đơn hàng
    $(document).on('click', '.btn-cancel', function () {
        var orderId = $(this).data('id');

        if (confirm('Bạn có chắc chắn muốn hủy đơn hàng này không?')) {
            $.ajax({
                url: '/Cart/CancelOrder',
                type: 'POST',
                data: { orderId: orderId },
                success: function (response) {
                    if (response.success) {
                        showMessage(response.message, "success");
                        // Gọi updateOrderCount() để cập nhật badge số lượng đơn hàng
                        updateOrderCount();
                        // Cập nhật trạng thái trên giao diện
                        var row = $(`tr[data-order-id="${orderId}"]`);
                        row.find('.order-status').text('Cancelled'); // Cập nhật cột Trạng thái
                        // Cập nhật cột Hủy đơn để khớp với logic trong HTML
                        row.find('.cancel-action').html('<span class="custom-text-muted text-success">Đã hủy</span>');
                    } else {
                        showMessage(response.message, "danger");
                    }
                },
                error: function (xhr) {
                    console.error('[DEBUG] AJAX Error:', xhr.status, xhr.statusText, xhr.responseText);
                    alert('Đã xảy ra lỗi khi hủy đơn hàng! Vui lòng kiểm tra console để biết thêm chi tiết.');
                }
            });
        }
    });

    // Hàm cập nhật số lượng giỏ hàng
    function updateCartCount() {
        $.ajax({
            url: '/Cart/GetCartCount',
            type: 'GET',
            success: function (response) {
                if (response.success) {
                    $('.cart-count').text(response.cartItemCount).addClass('bounce');
                    setTimeout(() => $('.cart-count').removeClass('bounce'), 500);
                }
            },
            error: function () {
                console.error('Không thể cập nhật số lượng giỏ hàng');
            }
        });
    }

    //Hàm cập nhật số lượng đặt hàng
    function updateOrderCount() {
        $.ajax({
            url: '/Cart/GetOrderCount',
            type: 'GET',
            success: function (response) {
                console.log('[DEBUG] Phản hồi từ GetOrderCount:', response);
                if (response.success) {
                    $('.shopping-count').text(response.orderCount);
                } else {
                    $('.shopping-count').text(0);
                }
            },
            error: function (xhr) {
                console.error('[DEBUG] AJAX Error in GetOrderCount:', xhr.status, xhr.statusText, xhr.responseText);
                $('.shopping-count').text(0);
            }
        });
    }
    function updateFavCount() {
        $.ajax({
            url: '/Fav/GetFavCount',
            type: 'GET',
            success: function (response) {
                if (response.success) {
                    $('.fav-count').text(response.wishlistcount).addClass('bounce');
                    setTimeout(() => $('.fav-count').removeClass('bounce'), 500);
                }
            },
            error: function () {
                console.error('Không thể cập nhật số lượng giỏ hàng');
            }
        });
    }

    // Gọi updateCartCount() ngay khi trang load
    updateCartCount();

    // Gọi updateOrderCount() ngay khi trang load
    updateOrderCount();

    updateFavCount();

    // Xóa giỏ hàng
    $(document).on('click', '.btn-remove', function () {
        var cartItemId = $(this).data('id');
        $.ajax({
            url: '/Cart/RemoveFromCart',
            type: 'POST',
            data: { cartItemId: cartItemId },
            success: function (response) {
                if (response.success) {
                    location.reload();
                } else {
                    alert(response.message);
                }
            },
            error: function () {
                alert('Đã xảy ra lỗi khi xóa sản phẩm!');
            }
        });
    });

    // Xóa ưa thích
    $(document).on('click', '.remove-fav-btn', function () {
        console.log("Batman is cleaning the streets...");

        var btn = $(this);
        var productid = btn.data('id');
        var productItem = btn.closest('.col-lg-3');

        $.ajax({
            url: '/Fav/RemoveFromFav',
            type: 'POST',
            data: { productid: productid },
            success: function (response) {
                if (response.success) {
                    // Hiệu ứng biến mất mượt mà
                    productItem.fadeOut(200, function () {
                        $(this).remove();

                        // Nếu xóa xong mà không còn sp nào thì hiện thông báo trống
                        if ($('#productContainer').children().length === 0) {
                            $('#productContainer').html('<div class="col-12 text-center"><h3>Danh sách yêu thích trống!</h3></div>');
                        }
                    });

                    // Cập nhật lại số lượng trên icon trái tim ở Header
                    // Bạn có thể dùng hàm updateFavCount() cũ hoặc lấy từ response nếu có
                    if (response.wishlistCount !== undefined) {
                        $('.fav-count').text(response.wishlistCount);
                    } else {
                        updateFavCount();
                    }

                } else {
                    alert(response.message);
                }
            },
            error: function () {
                alert('Đã xảy ra lỗi khi xóa sản phẩm!');
            }
        });
    });


  
});



// =========================================================================
// HÀM TIỆN ÍCH CHUNG
// =========================================================================

// Xem trước ảnh
function previewImage(input) {
    const previewFile = (input && input.type === 'file')
        ? ((input.files && input.files[0]) || input._droppedFile)
        : null;

    if (previewFile) {
        const reader = new FileReader();
        reader.onload = function (e) {
            let preview = null;
            let uploadInstruction = null;
            const uploadBox = input.closest('.upload-box');
            if (uploadBox) {
                uploadInstruction = uploadBox.querySelector('.upload-instruction');
            }
            if (input.classList.contains('avatar-input')) {
                switch (input.id) {
                    case 'AvatarFile': preview = document.querySelector('#avatarPreview'); break;
                    case 'NewAvatar': preview = document.querySelector('#newAvatarPreview'); break;
                    case 'ProductImage1': preview = document.querySelector('#productImage1Preview'); break;
                    case 'ProductImage2': preview = document.querySelector('#productImage2Preview'); break;
                    case 'ProductImage3': preview = document.querySelector('#productImage3Preview'); break;
                }
            } else if (uploadBox) {
                preview = uploadBox.querySelector('.img-preview');
            }
            if (preview) {
                preview.src = e.target.result;
                preview.classList.remove('d-none');
                preview.style.display = 'block';
                if (uploadInstruction) uploadInstruction.style.display = 'none';
            } else {
                console.error("Không tìm thấy phần tử ảnh để xem trước cho input: ", input.id);
            }
        };
        reader.readAsDataURL(previewFile);
    }
}

// Xem trước ảnh banner
function previewBannerImage(input) {
    const previewFile = (input && input.type === 'file')
        ? ((input.files && input.files[0]) || input._droppedFile)
        : null;

    if (previewFile) {
        const reader = new FileReader();
        reader.onload = function (e) {
            const bannerArea = input.closest('.banner-upload-area');
            const preview = bannerArea ? bannerArea.querySelector('#bannerPreview, .banner-image-preview') : null;
            const uploadHint = bannerArea ? bannerArea.querySelector('.banner-upload-hint') : null;
            if (preview) {
                preview.src = e.target.result;
                preview.classList.remove('hidden');
                preview.style.display = 'block';
                if (uploadHint) {
                    uploadHint.style.display = 'none';
                }
            } else {
                console.error("Không tìm thấy banner preview");
            }
        };
        reader.readAsDataURL(previewFile);
    }
}

function assignDroppedFiles(input, files) {
    if (!input || !files || files.length === 0) {
        return false;
    }

    input._droppedFile = files[0];

    try {
        const transfer = new DataTransfer();
        transfer.items.add(files[0]);
        input.files = transfer.files;
        input.dispatchEvent(new Event('change', { bubbles: true }));
        return true;
    } catch (err) {
        // Fallback for environments where DataTransfer constructor is blocked.
        console.warn('DataTransfer is not available, using fallback dropped file storage.', err);
        input.dispatchEvent(new Event('change', { bubbles: true }));
        return true;
    }
}

function getUrlFromDroppedData(dataTransfer) {
    if (!dataTransfer) {
        return null;
    }

    const uriList = dataTransfer.getData('text/uri-list');
    if (uriList) {
        const uriCandidate = uriList
            .split('\n')
            .map(line => line.trim())
            .find(line => line && !line.startsWith('#'));

        if (uriCandidate) {
            return uriCandidate;
        }
    }

    const plainText = dataTransfer.getData('text/plain');
    if (plainText && /^https?:\/\//i.test(plainText.trim())) {
        return plainText.trim();
    }

    const htmlText = dataTransfer.getData('text/html');
    if (htmlText) {
        const match = htmlText.match(/src=["'](https?:\/\/[^"']+)["']/i);
        if (match && match[1]) {
            return match[1];
        }
    }

    return null;
}

async function assignImageFromUrlToInput(url, input) {
    if (!url || !input) {
        return false;
    }

    try {
        const response = await fetch('/admin/fetch-image-from-url', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ url: url })
        });

        if (!response.ok) {
            return false;
        }

        const blob = await response.blob();
        if (!blob.type.startsWith('image/')) {
            return false;
        }

        const fileName = (url.split('/').pop() || 'web-image.jpg').split('?')[0];
        const file = new File([blob], fileName, { type: blob.type });
        return assignDroppedFiles(input, [file]);
    } catch (error) {
        console.error('Không thể lấy ảnh từ URL kéo thả:', error);
        return false;
    }
}

function setupDragDropArea(dropArea, input, onDropCallback) {
    if (!dropArea || !input) {
        return;
    }

    const preventDefaults = (e) => {
        e.preventDefault();
        e.stopPropagation();
    };

    ['dragenter', 'dragover'].forEach(eventName => {
        dropArea.addEventListener(eventName, (e) => {
            preventDefaults(e);
            dropArea.classList.add('drag-over');
        });
    });

    ['dragleave', 'dragend', 'drop'].forEach(eventName => {
        dropArea.addEventListener(eventName, (e) => {
            preventDefaults(e);
            dropArea.classList.remove('drag-over');
        });
    });

    const handleDrop = async (e) => {
        preventDefaults(e);

        const files = Array.from(e.dataTransfer?.files ?? []).filter(file => file.type.startsWith('image/'));
        let processed = false;

        if (files.length > 0) {
            processed = assignDroppedFiles(input, files);
        }

        if (!processed) {
            const droppedUrl = getUrlFromDroppedData(e.dataTransfer);
            if (droppedUrl) {
                processed = await assignImageFromUrlToInput(droppedUrl, input);
            }
        }

        if (processed && typeof onDropCallback === 'function') {
            onDropCallback(input);
        } else if (!processed && typeof showMessage === 'function') {
            showMessage('Không thể xử lý dữ liệu kéo thả. Hãy kéo file ảnh từ máy hoặc URL ảnh trực tiếp.', 'danger');
        }
    };

    dropArea.addEventListener('drop', handleDrop);

    input.addEventListener('drop', () => {
        setTimeout(() => {
            if (input.files && input.files.length > 0) {
                input.dispatchEvent(new Event('change', { bubbles: true }));
            }
        }, 0);
    });

    input.addEventListener('drop', handleDrop);
}

function isUploadDropTarget(target) {
    if (!target || typeof target.closest !== 'function') {
        return false;
    }

    return !!target.closest('.upload-box, .banner-upload-area');
}

// Prevent browser from opening dropped files/urls in a new tab.
document.addEventListener('dragover', (e) => {
    e.preventDefault();
    if (e.dataTransfer) {
        e.dataTransfer.dropEffect = isUploadDropTarget(e.target) ? 'copy' : 'none';
    }
}, true);

document.addEventListener('drop', (e) => {
    e.preventDefault();
    if (!isUploadDropTarget(e.target)) {
        e.stopPropagation();
    }
}, true);


// Load danh mục phụ theo danh mục chính
function loadSubCategories(categorySelector, subCategorySelector) {
    const categoryId = $(categorySelector).val();
    $(subCategorySelector).empty().append('<option value="">-- Chọn danh mục phụ --</option>');
    if (categoryId) {
        $.ajax({
            url: `/product/get-sub-categories?categoryId=${categoryId}`,
            type: "GET",
            success: (data) => {
                $.each(data, (index, subCategory) => {
                    $(subCategorySelector).append(`<option value="${subCategory.id}">${subCategory.subCategoryName}</option>`);
                });
            },
            error: (xhr) => {
                console.error("Lỗi lấy danh mục phụ:", xhr.responseText);
                alert("Không thể tải danh mục phụ!");
            }
        });
    }
}


// =========================================================================
// XỬ LÝ ẢNH KHI TẢI TRANG VÀ SỰ KIỆN CHANGE
// =========================================================================
document.querySelectorAll('.avatar-input, .file-input').forEach(input => {
    if (input.type === 'file') {
        // Sự kiện change khi chọn file
        input.addEventListener('change', () => {
            if (input.files && input.files.length > 0) {
                input._droppedFile = null;
            }

            previewImage(input);
        });

        // Kiểm tra ảnh mặc định khi tải trang
        const uploadBox = input.closest('.upload-box');
        if (uploadBox) {
            let preview = null;
            switch (input.id) {
                case 'AvatarFile': preview = document.querySelector('#avatarPreview'); break;
                case 'NewAvatar': preview = document.querySelector('#newAvatarPreview'); break;
                case 'ProductImage1': preview = document.querySelector('#productImage1Preview'); break;
                case 'ProductImage2': preview = document.querySelector('#productImage2Preview'); break;
                case 'ProductImage3': preview = document.querySelector('#productImage3Preview'); break;
                default: preview = uploadBox.querySelector('.img-preview');
            }
            const uploadInstruction = uploadBox.querySelector('.upload-instruction');
            if (preview && uploadInstruction) {
                const isDefaultImage = preview.src.includes('default-product.png') || preview.src.includes('default-avatar.png');
                if (preview.src && !isDefaultImage) {
                    preview.style.display = 'block';
                    preview.classList.remove('d-none');
                    uploadInstruction.style.display = 'none';
                } else {
                    preview.style.display = 'none';
                    preview.classList.add('d-none');
                    uploadInstruction.style.display = 'block';
                }
            }
        }
    } else {
        console.warn("Input không phải là type='file': ", input.id, input.className);
    }
});

document.querySelectorAll('.upload-box').forEach(uploadBox => {
    const input = uploadBox.querySelector('.avatar-input, .file-input');
    if (!input) {
        return;
    }

    setupDragDropArea(uploadBox, input, previewImage);
});

//Banner
document.querySelectorAll('.banner-file-input').forEach(bannerInput => {
    bannerInput.addEventListener('change', function () {
        if (bannerInput.files && bannerInput.files.length > 0) {
            bannerInput._droppedFile = null;
        }

        previewBannerImage(this);
    });

    const bannerArea = bannerInput.closest('.banner-upload-area');
    if (bannerArea) {
        const preview = bannerArea.querySelector('#bannerPreview, .banner-image-preview');
        const uploadHint = bannerArea.querySelector('.banner-upload-hint');
        if (preview && uploadHint) {
            const isDefaultImage = preview.src.includes('default-product.png') || preview.src.includes('default-avatar.png');
            if (preview.src && !isDefaultImage) {
                preview.style.display = 'block';
                preview.classList.remove('hidden');
                uploadHint.style.display = 'none';
            } else {
                preview.style.display = 'none';
                preview.classList.add('hidden');
                uploadHint.style.display = 'block';
            }
        }

        setupDragDropArea(bannerArea, bannerInput, previewBannerImage);
    }
});



// =========================================================================
// KHỞI TẠO KHI TẢI TRANG
// =========================================================================

// Ẩn banner khi ở trang login
//Do sử dụng Json nên ko thể sử dụng được viewBag 
    if (window.location.pathname.toLowerCase() === "/userauth/login") {
        $("#showbanner").hide();
    }
// Khởi tạo LC Lightbox
$(function () {
    lc_lightbox('.elem', {
        wrap_class: 'lcl_fade_oc',
        gallery: true,
        thumb_attr: 'data-lcl-thumb',
        skin: 'dark',
        fullscreen: true,
        download: true,
        socials: true
    });
});

// Khởi tạo AOS
AOS.init();

// Load danh mục phụ cho form chỉnh sửa sản phẩm
if ($("#editCategoryId").length > 0) {
    loadSubCategories("#editCategoryId", "#editSubCategoryId");
}


// =========================================================================
// SỰ KIỆN GIAO DIỆN
// =========================================================================
$('.product-carousel').owlCarousel({
    loop: true,
    margin: 8,
    nav: false,
    dots: false,
    responsiveClass: true,
    responsive: {
        0: {
            items: 2,
            nav: true
        },
        600: {
            items: 2,
            nav: false
        },
        1000: {
            items: 5,
            nav: true,
            loop: false
        }
    }
})

$(".main-banner-carousel").owlCarousel({
    autoplay: true,
    autoplayTimeout: 3000,
    loop: true,
    margin: 10,
    slideTransition: 'linear',
    autoplaySpeed: 1000,
    smartSpeed: 800,
    responsiveClass: true,
    responsive: {
        0: {
            items: 1,
            nav: true,
            dots: true,
        },
        600: {
            items: 1,
        },
        1000: {
            items: 1,
            nav: true,
            dots: true,
        }
    }
});

//Overlay
$(function () {
    // Áp dụng overlay cho toàn trang
    const $body = $('body');
    let isLoading = false;

    // Hàm hiển thị overlay
    function showLoadingOverlay() {
        if (!isLoading) {
            isLoading = true;
            $body.loadingOverlay(true, {
                backgroundColor: 'rgba(0, 0, 0, 0.7)',
                imageColor: '#007bff',
                fade: [2000, 4000],
                zIndex: 9999
            });
        }
    }

    // Xử lý tất cả các liên kết lọc (category, subcategory, sort)
    $('.category-link, .subcategory-link, .sort-link').on('click', function (e) {
        e.preventDefault();
        if (isLoading) return; // Ngăn nhấp liên tục khi đang tải

        const href = $(this).attr('href');
        showLoadingOverlay();
        setTimeout(() => {
            window.location.href = href; // Chuyển hướng sau khi overlay hiển thị
        }, 100); // Delay nhẹ để overlay xuất hiện trước khi chuyển trang
    });

    // Xử lý form tìm kiếm
    $('#searchForm').on('submit', function (e) {
        if (isLoading) {
            e.preventDefault();
            return;
        }
        showLoadingOverlay();
    });

    // Cập nhật danh sách tiểu danh mục khi chọn danh mục
    $('.category-link').on('click', function (e) {
        const categoryId = $(this).data('category-id') || '';
        const $subCategorySection = $('#subCategorySection');

        if (categoryId && !isLoading) {
            $.get('/Shop/GetSubCategories', { categoryId: categoryId }, function (data) {
                let html = '<h5 class="font-weight-semi-bold mb-4">Filter by SubCategory</h5>' +
                    '<div class="list-group">' +
                    `<a href="@Url.Action("Index", "Shop", new { categoryId = "${categoryId}", subCategoryId = "", search = "${searchTerm}", sortBy = "${sortBy}" })" ` +
                    'class="list-group-item list-group-item-action subcategory-link" data-subcategory-id="">All SubCategories</a>';

                $.each(data, function (index, item) {
                    html += `<a href="@Url.Action("Index", "Shop", new { categoryId = "${categoryId}", subCategoryId = "${item.id}", search = "${searchTerm}", sortBy = "${sortBy}" })" ` +
                        `class="list-group-item list-group-item-action subcategory-link" data-subcategory-id="${item.id}">${item.name}</a>`;
                });

                html += '</div>';
                $subCategorySection.html(html).fadeIn(300); // Thêm hiệu ứng fade
            });
        } else if (!categoryId) {
            $subCategorySection.fadeOut(300); // Ẩn với hiệu ứng fade
        }
    });


});


//Mega menu
$(function () { 
    "use strict";

    // Thêm class và icon cho menu có submenu
    $('.menu > ul > li').filter(':has(> ul)').addClass('menu-dropdown-icon').find('> a').append('<i class="ri-add-box-line"></i>');
    $('.menu > ul > li > ul').filter(':not(:has(ul))').addClass('normal-sub');
    $(".menu > ul").before('<a href="#" class="menu-mobile"><i class="ri-menu-fill"></i> LAPTOP|BD</a>');

    // Hover cho desktop (sử dụng .on thay .hover)
    $(".menu > ul > li").on("mouseenter mouseleave", function (e) {
        if ($(window).width() > 943) {
            $(this).children("ul").stop(true, true).fadeToggle(150); // Giữ stop(true, true) để ngăn xếp hàng
            e.preventDefault();
        }
    });

    // Click cho mobile (sử dụng .on thay .click)
    $(".menu > ul > li").on("click", function () {
        if ($(window).width() <= 943) {
            $(this).children("ul").stop(true, true).slideToggle(300);
        }
    });

    // Toggle menu mobile
    $(".menu-mobile").on("click", function (e) {
        $(".menu > ul").stop(true, true).slideToggle(300).toggleClass('show-on-mobile');
        e.preventDefault();
    });
});

//Toggle của login and / register user
function clearSignupForm() {
    const fieldIds = ['SignupEmail', 'Username', 'Phone', 'SignupPassword', 'SignupConfirmPassword'];
    fieldIds.forEach(function (id) {
        const el = document.getElementById(id);
        if (el) {
            el.value = '';
        }
    });

    const signupMessage = document.getElementById('signup-message');
    if (signupMessage) {
        signupMessage.innerHTML = '';
    }

    if (typeof updateSignupPasswordChecklist === 'function') {
        updateSignupPasswordChecklist('');
    }
}

function toggleSignup() {
    document.getElementById("login-toggle").style.backgroundColor = "#fff";
    document.getElementById("login-toggle").style.color = "#222";
    document.getElementById("signup-toggle").style.backgroundColor = "#3a79ff";
    document.getElementById("signup-toggle").style.color = "#fff";
    document.getElementById("login-form").style.display = "none";
    if (document.getElementById("verify-email-form")) {
        document.getElementById("verify-email-form").style.display = "none";
    }
    if (document.getElementById("forgot-password-form")) {
        document.getElementById("forgot-password-form").style.display = "none";
    }
    clearSignupForm();
    document.getElementById("signup-form").style.display = "block";
}
function toggleLogin() {
    document.getElementById("login-toggle").style.backgroundColor = "#3a79ff";
    document.getElementById("login-toggle").style.color = "#fff";
    document.getElementById("signup-toggle").style.backgroundColor = "#fff";
    document.getElementById("signup-toggle").style.color = "#222";
    document.getElementById("signup-form").style.display = "none";
    if (document.getElementById("verify-email-form")) {
        document.getElementById("verify-email-form").style.display = "none";
    }
    if (document.getElementById("forgot-password-form")) {
        document.getElementById("forgot-password-form").style.display = "none";
    }
    document.getElementById("login-form").style.display = "block";
}

document.addEventListener('DOMContentLoaded', function () {
    if (typeof clearSignupForm === 'function') {
        clearSignupForm();
    }
});

window.addEventListener('pageshow', function () {
    if (typeof clearSignupForm === 'function') {
        clearSignupForm();
    }
});

// Hàm hiển thị/ẩn danh mục con
function showSubCategories(categoryId) {
    const subCategorySection = document.getElementById('subCategorySection');
    if (categoryId && categoryId !== '') {
        subCategorySection.style.display = 'block';
    } else {
        subCategorySection.style.display = 'none';
    }
}

