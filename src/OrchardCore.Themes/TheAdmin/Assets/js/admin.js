function confirmDialog(title, message, okText, cancelText, okCssClass, cancelCssClass, handler) {
    if (title === undefined) {
        title = $('#confirmRemoveModalMetadata').data('title');
    }

    if (message === undefined) {
        message = $('#confirmRemoveModalMetadata').data('message');
    }

    if (okText === undefined) {
        okText = $('#confirmRemoveModalMetadata').data('ok');
    }

    if (cancelText === undefined) {
        cancelText = $('#confirmRemoveModalMetadata').data('cancel');
    }

    if (okCssClass === undefined) {
        okCssClass = $('#confirmRemoveModalMetadata').data('okClass');
    }

    if (cancelCssClass === undefined) {
        cancelCssClass = $('#confirmRemoveModalMetadata').data('cancelClass');
    }

    $('<div id="confirmRemoveModal" class="modal" tabindex="-1" role="dialog">\
        <div class="modal-dialog modal-dialog-centered" role="document">\
            <div class="modal-content">\
                <div class="modal-header">\
                    <h5 class="modal-title">' + title + '</h5>\
                    <button type="button" class="close" data-dismiss="modal" aria-label="Close">\
                        <span aria-hidden="true">&times;</span>\
                    </button>\
                </div>\
                <div class="modal-body">\
                    <p>' + message +'</p>\
                </div>\
                <div class="modal-footer">\
                    <button id="modalOkButton" type="button" class="btn ' + okCssClass + '">' + okText + '</button>\
                    <button id="modalCancelButton" type="button" class="btn' + cancelCssClass + '" data-dismiss="modal">' + cancelText + '</button>\
                </div>\
            </div>\
        </div>\
    </div>').appendTo("body");
    $("#confirmRemoveModal").modal({
        backdrop: 'static',
        keyboard: false
    });

    $("#confirmRemoveModal").on('hidden.bs.modal', function () {
        $("#confirmRemoveModal").remove();
    });

    $("#modalOkButton").click(function () {
        handler(true);
        $("#confirmRemoveModal").modal("hide");
    });

    $("#modalCancelButton").click(function () {
        handler(false);
        $("#confirmRemoveModal").modal("hide");
    });
}

$(function () {
    $("body").removeClass("preload");
});

$(function () {
    $("body").on("click", "[itemprop~='RemoveUrl']", function () {
        var _this = $(this);
        // don't show the confirm dialog if the link is also UnsafeUrl, as it will already be handled below.
        if (_this.filter("[itemprop~='UnsafeUrl']").length == 1) {
            return false;
        }
        // use a custom message if its set in data-message
        var title = _this.data('title');
        var message = _this.data('message');
        var okText = _this.data('ok');
        var cancelText = _this.data('cancel');
        var okCssClass = _this.data('okClass');
        var cancelCssClass = _this.data('cancelClass');
        confirmDialog(title, message, okText, cancelText, okCssClass, cancelCssClass, function(resp) {
            if (resp) {
                var url = _this.attr('href');
                if (url == undefined) {
                    var form = _this.parents('form');
                    // This line is reuired in case we used the FormValueRequiredAttribute
                    form.append($("<input type=\"hidden\" name=\"" + _this.attr('name') + "\" value=\"" + _this.attr('value') + "\" />"));
                    form.submit();
                }
                else {
                    window.location = url;
                }
            }
        });

        return false;
    });
});

$(function () {
    var magicToken = $("input[name=__RequestVerificationToken]").first();
    if (magicToken) {
        $("body").on("click", "a[itemprop~='UnsafeUrl'], a[data-unsafe-url]", function () {
            var _this = $(this);
            var hrefParts = _this.attr("href").split("?");
            var form = $("<form action=\"" + hrefParts[0] + "\" method=\"POST\" />");
            form.append(magicToken.clone());
            if (hrefParts.length > 1) {
                var queryParts = hrefParts[1].split("&");
                for (var i = 0; i < queryParts.length; i++) {
                    var queryPartKVP = queryParts[i].split("=");
                    //trusting hrefs in the page here
                    form.append($("<input type=\"hidden\" name=\"" + decodeURIComponent(queryPartKVP[0]) + "\" value=\"" + decodeURIComponent(queryPartKVP[1]) + "\" />"));
                }
            }
            form.css({ "position": "absolute", "left": "-9999em" });
            $("body").append(form);

            var unsafeUrlPrompt = _this.data("unsafe-url");
            var title = _this.data("title");
            var message = unsafeUrlPrompt;
            var okText = _this.data('ok');
            var cancelText = _this.data('cancel');
            var okCssClass = _this.data('okClass');
            var cancelCssClass = _this.data('cancelClass');

            if (unsafeUrlPrompt && unsafeUrlPrompt.length > 0) {
                confirmDialog(title, unsafeUrlPrompt, okText, cancelText, okCssClass, cancelCssClass, function(resp) {
                    if (resp) {
                        form.submit();
                    }
                });

                return false;
            }

            if (_this.filter("[itemprop~='RemoveUrl']").length == 1) {
                message = _this.data('message');
                confirmDialog(title, message, okText, cancelText, okCssClass, cancelCssClass, function(resp) {
                    if (resp) {
                        form.submit();
                    }
                });

                return false;
            }

            form.submit();
            return false;
        });
    }
});

$(function () {
    $('input[data-toggle="collapse"]').each(function () {
        // Prevent bootstrap from altering its behavior
        // c.f. https://github.com/twbs/bootstrap/issues/21079
        $(this).removeAttr("data-toggle");

        // Expand the section if necessary
        var target = $($(this).data('target'));
        if ($(this).prop('checked')) {
            target.addClass('show');
        }

        $(this).on('change', function (e) {
            // During a double-click, ignore state changes while the element is collapsing
            if (target.hasClass('collapsing')) {
                $(this).prop('checked', !$(this).prop('checked'));
            }
            target.collapse($(this).prop('checked') ? 'show' : 'hide');
        });
    });
});

$(function () {
    $('input[data-toggle="collapse active"]').each(function () {
        // Prevent bootstrap from altering its behavior for inputs that hide target when input value is checked
        // c.f. https://github.com/twbs/bootstrap/issues/21079
        $(this).removeAttr("data-toggle");

        // Expand the section if necessary
        var target = $($(this).data('target'));
        if (!$(this).prop('checked')) {
            target.addClass('show');
        }

        $(this).on('change', function (e) {
            // During a double-click, ignore state changes while the element is collapsing
            if (target.hasClass('collapsing')) {
                console.log('collapsing');
                $(this).prop('checked', !$(this).prop('checked'));
            }
            target.collapse($(this).prop('checked') ? 'hide' : 'show');
        });
    });

});

function getTechnicalName(name) {
    var result = "", c;

    if (!name || name.length == 0) {
        return "";
    }

    name = removeDiacritics(name);

    for (i = 0; i < name.length; i++) {
        c = name[i];
        if (isLetter(c) || (isNumber(c) && i > 0)) {
            result += c;
        }
    }

    return result;
}

function isLetter(str) {
    return str.length === 1 && str.match(/[a-z]/i);
}

function isNumber(str) {
    return str.length === 1 && str.match(/[0-9]/i);
}