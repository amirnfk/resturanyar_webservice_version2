// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// تعریف توکن ثابت
const STATIC_TOKEN = "stR@nG3_Stat1c_T0ken_Resturanyar_2025!#X9LpQ";

// تنظیم global برای همه AJAX call های jQuery
$.ajaxSetup({
    headers: {
        "Authorization": "Bearer " + STATIC_TOKEN
    },
    contentType: "application/json",
    error: function (xhr, status, error) {
        console.error("AJAX Error:", xhr.responseText);
        alert("خطا در ارتباط با سرور: " + xhr.status);
    }
});
 
!function () { var i ="c6rsQH",a=window,d=document;function g(){var g=d.createElement("script"),s="https://www.goftino.com/widget/"+i,l=localStorage.getItem("goftino_"+i);g.async=!0,g.src=l?s+"?o="+l:s;d.getElementsByTagName("head")[0].appendChild(g);}"complete"===d.readyState?g():a.attachEvent?a.attachEvent("onload",g):a.addEventListener("load",g,!1);}();
 


 
        window.addEventListener('goftino_ready', function () {
            document.getElementById("open_chat").addEventListener("click", function () {
                Goftino.setUser({
                    email: '...',
                    name: '...',
                    about: 'Resturanyar...',
                    phone: '...',
                    avatar: '...',

                    forceUpdate: true
                });

                Goftino.open();
            });
        document.getElementById("close_chat").addEventListener("click", function () {
            Goftino.close();
        });
        document.getElementById("toggle_chat").addEventListener("click", function () {
            Goftino.toggle();
        });
    });
 