/* CW UI 覆盖层：主菜单小贴士 + 大版本更新提示。
   由插件宿主以 <script defer> 注入；DOM 挂到 document.body，
   避免受 .hud（transform 包含块）与页面切换影响。 */
(function () {
    'use strict';
    if (window.cwUi) return;

    // 记录最近的 JS/资源加载错误，供插件组件通过 cwUi.lastError() 诊断
    window.addEventListener('error', function (e) {
        var t = e.target;
        if (t && t.tagName === 'SCRIPT')
            window.__cwErr = 'script load failed: ' + (t.src || '');
        else
            window.__cwErr = (e.message || 'error') + ' @' + (e.filename || '') + ':' + (e.lineno || 0);
    }, true);

    var root = null;
    var tipsBox = null;
    var tipsText = null;
    var tipsTimer = null;
    var tips = [];
    var tipsIndex = 0;
    var modal = null;

    function ensureRoot() {
        if (root && document.body.contains(root)) return root;
        root = document.getElementById('cw-overlay-root');
        if (!root) {
            root = document.createElement('div');
            root.id = 'cw-overlay-root';
            document.body.appendChild(root);
        }
        return root;
    }

    function esc(value) {
        return String(value == null ? '' : value).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function stopTips() {
        if (tipsTimer) {
            window.clearInterval(tipsTimer);
            tipsTimer = null;
        }
    }

    function renderTip() {
        if (!tipsText || tips.length === 0) return;
        tipsText.textContent = tips[tipsIndex % tips.length];
        tipsIndex = (tipsIndex + 1) % tips.length;
    }

    function showTips(items, intervalMs) {
        var host = ensureRoot();
        stopTips();
        tips = (items || []).filter(function (t) { return t && String(t).trim().length > 0; });
        if (tips.length === 0) {
            hideTips();
            return;
        }

        if (!tipsBox) {
            tipsBox = document.createElement('div');
            tipsBox.className = 'cw-tips';
            tipsBox.innerHTML = '<span class="cw-tips-ic">◆</span><span class="cw-tips-tx"></span>';
            host.appendChild(tipsBox);
            tipsText = tipsBox.querySelector('.cw-tips-tx');
        }

        tipsIndex = 0;
        tipsBox.style.display = 'flex';
        renderTip();

        var delay = intervalMs > 0 ? intervalMs : 5000;
        if (tips.length > 1) {
            tipsTimer = window.setInterval(renderTip, delay);
        }
    }

    function hideTips() {
        stopTips();
        if (tipsBox) tipsBox.style.display = 'none';
    }

    function closeUpdate() {
        if (modal && modal.parentNode) modal.parentNode.removeChild(modal);
        modal = null;
    }

    function showUpdate(dotnetRef, title, body, url1, url2, dl1Label, dl2Label, backLabel) {
        closeUpdate();
        var host = ensureRoot();
        var urls = [url1 || '', url2 || ''];
        var links = '';

        if (urls[0]) {
            links += '<button type="button" class="cw-upd-link" data-url="0">' + esc(dl1Label || 'Download link 1') + '<span>' + esc(urls[0]) + '</span></button>';
        }
        if (urls[1]) {
            links += '<button type="button" class="cw-upd-link" data-url="1">' + esc(dl2Label || 'Download link 2') + '<span>' + esc(urls[1]) + '</span></button>';
        }

        modal = document.createElement('div');
        modal.className = 'cw-upd-back';
        modal.innerHTML =
            '<div class="cw-upd" role="dialog" aria-modal="true">' +
            '<div class="cw-upd-head"><span class="cw-upd-ic">!</span><b class="cw-upd-title">' + esc(title) + '</b></div>' +
            '<div class="cw-upd-body">' + esc(body) + '</div>' +
            '<div class="cw-upd-links">' + links + '</div>' +
            '<div class="cw-upd-foot"><button type="button" class="cw-upd-close">' + esc(backLabel || 'Back') + '</button></div>' +
            '</div>';

        host.appendChild(modal);

        var linkNodes = modal.querySelectorAll('.cw-upd-link');
        for (var i = 0; i < linkNodes.length; i++) {
            (function (node) {
                node.addEventListener('click', function () {
                    var url = urls[parseInt(node.getAttribute('data-url'), 10)];
                    if (!url) return;
                    if (dotnetRef && dotnetRef.invokeMethodAsync) {
                        dotnetRef.invokeMethodAsync('OpenUrl', url);
                    } else {
                        window.open(url, '_blank');
                    }
                });
            })(linkNodes[i]);
        }

        var closeButton = modal.querySelector('.cw-upd-close');
        if (closeButton) closeButton.addEventListener('click', closeUpdate);
    }

    /* 按顺序加载外部脚本（幂等）：用于战役沙盘的本地 three.js 与场景脚本按需注入。
       参数逐个传入（避免数组跨 JSInterop 序列化差异）。 */
    function loadScripts() {
        var list = Array.prototype.slice.call(arguments).filter(function (s) { return !!s; });
        return list.reduce(function (pending, src) {
            return pending.then(function () {
                if (document.querySelector('script[data-cw-src="' + src + '"]')) return;
                return new Promise(function (resolve, reject) {
                    var el = document.createElement('script');
                    el.src = src;
                    el.async = false;
                    el.setAttribute('data-cw-src', src);
                    el.onload = function () { resolve(); };
                    el.onerror = function () { reject(new Error('script load failed: ' + src)); };
                    document.head.appendChild(el);
                });
            });
        }, Promise.resolve());
    }

    window.cwUi = {
        ping: function () { },
        showTips: showTips,
        hideTips: hideTips,
        showUpdate: showUpdate,
        closeUpdate: closeUpdate,
        loadScripts: loadScripts,
        lastError: function () { return window.__cwErr || ''; }
    };
})();
