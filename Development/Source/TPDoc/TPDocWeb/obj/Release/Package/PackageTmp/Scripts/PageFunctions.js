$(".environmentTarget").click(function myfunction()
{
    var env = $(this).text();
    $.ajax({
        url: "/Wiki/EnvironmentPage/" + env + "/" + $("#hiddenPageId").text(),
        type: 'GET',
        async: true,
        success: function (data)
        {
            if (data === "failure")
            {
                alert("Something went wrong while sending the request to the API.");
            }
            else
            {
                $("#generatedContentPage").html(data);
                $("#environmentHeader").text(env);
            }
        }

    });

    return true;
})

$(document).ready(replaceCustomTag);
$("html").ajaxStop(replaceCustomTag);

RegExp.quote = function (str) {
    return (str + '').replace(/[.?*+^$[\]\\(){}|-]/g, "\\$&");
};

function replaceCustomTag() {
    var regex = /\[\%(.*)\%\]/g;

    var filteredMatches = [];
    var match;
    while (match = regex.exec($('.manualContent').text())) {
        filteredMatches.push(match[1]);
    }

    for (var i = 0; i < filteredMatches.length; i++)
    {
        var controllerUrl
        if (filteredMatches[i].indexOf("Custom£") == 0)
        {
            controllerUrl= "/Wiki/GetBlobContent/Production/" + filteredMatches[i];
        }
        else
        {
            controllerUrl = "/Wiki/GetBlobContent/Main/" + filteredMatches[i];
        }
        $.ajax({
            url: controllerUrl,
            type: 'GET',
            async: true,
            success: function (data) {
                var regex = new RegExp(RegExp.quote("\[\%" + data.pageId + "\%\]"));
                var t = regex.source;
                var s = $('.manualContent').html().match(t);
                $('.manualContent').html($('.manualContent').html().replace(s, data.content));
            }
        });
    }
}


$(document).ready(function () {
    var text = $('#hiddenSearchText').text();

    if (text.length > 2)
    {
        $('.container').wrapInTag({
            tag: 'span class="yellowText"',
            words: text.split(" ")
        });
    }
});

$(window).on("load", function()
{
    var text = $("#hiddenPhrase").text();
    if (text.length > 0)
    {
        var foundin = $('section:contains("' + text + '")');
        $("html, body").scrollTop($(foundin).offset().top);
    }
});

$.fn.wrapInTag = function (opts) {
    function getText(obj) {
        return obj.textContent ? obj.textContent : obj.innerText;
    }

    var tag = opts.tag || 'span class="yellowText"',
        words = opts.words || [],
        regex = RegExp(words.join('|'), 'gi'), //case insensitive
        replacement = '<' + tag + '>$&</' + tag + '>';

    $(this).contents().each(function () {
        if (this.nodeType === 3) //Node.TEXT_NODE
        {
            $(this).replaceWith(getText(this).replace(regex, replacement));
        }
        else if (!opts.ignoreChildNodes) {
            $(this).wrapInTag(opts);
        }
    });
};
