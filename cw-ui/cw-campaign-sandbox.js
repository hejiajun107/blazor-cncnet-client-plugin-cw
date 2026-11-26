/* CW UI 战役全息战术沙盘（three.js）。
   形态：程序化起伏地形 + 等高色带 + 阵营染色区 + 投影底座/光锥 + 3D 旗标 + 引线浮框。
   数据由 CwCampaignSandbox.razor 传入（JSON 字符串）：阵营 → 战役点位。 */
(function () {
    'use strict';
    if (window.cwCs) return;

    var reduced = !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);

    var canvas = null, stage = null, renderer = null, scene = null, camera = null;
    var raycaster = null, pointer = null, dotnet = null;
    var factions = [], markers = [];
    var terrainGroup = null, markerGroup = null, terrainMesh = null, flowMat = null;
    var terrainGeo = null, vertT = null, tintMats = [];
    var accentStroke = 'rgba(110,232,255,0.75)';
    var accentShadow = 'rgba(110,232,255,0.9)';
    var accentDot = 'rgba(140,240,255,0.95)';
    var linkCanvas = null, linkCtx = null, cardEl = null;
    var activeFaction = 0, selectedId = null;
    var azimuth = 0.6, polar = 1.02, radius = 15.0;
    var intro = 0, dragging = false, moved = false, lastX = 0, lastY = 0, hoverId = null;
    var raf = 0, ro = null, disposed = false, elapsed = 0;
    var tmpV = null;

    function clamp(v, lo, hi) { return v < lo ? lo : (v > hi ? hi : v); }
    function smoothstep(a, b, x) {
        var t = clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    /* ── 地形高度场（确定性值噪声 + fbm） ── */
    function hash2(x, z) {
        var n = Math.sin(x * 127.1 + z * 311.7) * 43758.5453;
        return n - Math.floor(n);
    }

    function vnoise(x, z) {
        var xi = Math.floor(x), zi = Math.floor(z);
        var xf = x - xi, zf = z - zi;
        var u = xf * xf * (3 - 2 * xf), v = zf * zf * (3 - 2 * zf);
        var a = hash2(xi, zi), b = hash2(xi + 1, zi);
        var c = hash2(xi, zi + 1), d = hash2(xi + 1, zi + 1);
        return a * (1 - u) * (1 - v) + b * u * (1 - v) + c * (1 - u) * v + d * u * v;
    }

    function fbm(x, z) {
        var s = 0, amp = 0.5, f = 1;
        for (var i = 0; i < 4; i++) {
            s += amp * vnoise(x * f, z * f);
            f *= 2.07;
            amp *= 0.5;
        }
        return s;
    }

    var BOARD = 8.0; // 半径

    function terrainH(x, z) {
        var d = Math.sqrt(x * x + z * z);
        var rim = 1 - smoothstep(6.6, BOARD + 0.4, d);
        var h = (fbm(x * 0.26 + 11.3, z * 0.26 + 7.1) - 0.42) * 2.4;
        // 中央制高点 + 环形山脊
        h += Math.exp(-(d * d) / 16) * 0.42;
        h -= Math.exp(-((d - 4.6) * (d - 4.6)) / 2.2) * 0.22;
        // 限制最低点，避免谷底/边缘点位低于底盘显得“掉下去”
        var v = h * rim;
        return v < -0.12 ? -0.12 : v;
    }

    // 默认阵营主色（Battle.ini 未配置 Color 时的地形基色）
    var BASE_ACCENT = { r: 0.22, g: 0.88, b: 1.0 };

    function palette(t, c) {
        // 低地深色 → 中段 → 高地亮色，整体随阵营主色走
        var col = c || BASE_ACCENT;
        var lo = [col.r * 0.10, col.g * 0.10, col.b * 0.10];
        var mid = [col.r * 0.42, col.g * 0.42, col.b * 0.42];
        var hi = [col.r * 0.62 + 0.34, col.g * 0.62 + 0.34, col.b * 0.62 + 0.34];
        return t < 0.55
            ? [lo[0] + (mid[0] - lo[0]) * (t / 0.55), lo[1] + (mid[1] - lo[1]) * (t / 0.55), lo[2] + (mid[2] - lo[2]) * (t / 0.55)]
            : [mid[0] + (hi[0] - mid[0]) * ((t - 0.55) / 0.45), mid[1] + (hi[1] - mid[1]) * ((t - 0.55) / 0.45), mid[2] + (hi[2] - mid[2]) * ((t - 0.55) / 0.45)];
    }

    function disposeObject(obj) {
        if (!obj) return;
        obj.traverse(function (o) {
            if (o.geometry) o.geometry.dispose();
            if (o.material) {
                if (Array.isArray(o.material)) o.material.forEach(function (m) { m.dispose(); });
                else o.material.dispose();
            }
        });
    }

    /* ── 地形实体 + 等高色带（顶点色随阵营主色重算） ── */
    function buildTerrain() {
        terrainGroup = new THREE.Group();
        scene.add(terrainGroup);
        tintMats = [];

        var seg = 110;
        var geo = new THREE.PlaneGeometry(BOARD * 2, BOARD * 2, seg, seg);
        geo.rotateX(-Math.PI / 2);

        var pos = geo.attributes.position;
        var colors = [];
        vertT = new Float32Array(pos.count);

        for (var i = 0; i < pos.count; i++) {
            var x = pos.getX(i), z = pos.getZ(i);
            var h = terrainH(x, z);
            pos.setY(i, h);

            var t = clamp((h + 0.5) / 1.7, 0, 1);
            vertT[i] = t;
            var base = palette(t, BASE_ACCENT);
            // 等高色带：量化高度，形成等高线层次
            var band = Math.floor(t * 9) / 9;
            var bc = palette(band, BASE_ACCENT);
            base = [base[0] * 0.55 + bc[0] * 0.45, base[1] * 0.55 + bc[1] * 0.45, base[2] * 0.55 + bc[2] * 0.45];

            colors.push(base[0], base[1], base[2]);
        }
        geo.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
        geo.computeVertexNormals();
        terrainGeo = geo;

        var mesh = new THREE.Mesh(geo, new THREE.MeshBasicMaterial({
            vertexColors: true, transparent: true, opacity: 0.96
        }));
        terrainMesh = mesh;
        terrainGroup.add(mesh);

        // 地形表面流动光带（低强度叠加，克制不抢主体）
        flowMat = new THREE.ShaderMaterial({
            uniforms: {
                uTime: { value: 0 },
                uColor: { value: new THREE.Color(0x49d8ff) }
            },
            vertexShader: [
                'varying vec3 vPos;',
                'void main() {',
                '  vPos = position;',
                '  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);',
                '}'
            ].join('\n'),
            fragmentShader: [
                'uniform float uTime;',
                'uniform vec3 uColor;',
                'varying vec3 vPos;',
                'void main() {',
                '  float rise = fract(vPos.y * 1.6 - uTime * 0.10);',
                '  float band = smoothstep(0.90, 1.0, rise) * 0.16;',
                '  float drift = 0.55 + 0.45 * sin(vPos.x * 0.7 + vPos.z * 0.55 - uTime * 0.5);',
                '  float a = band * drift;',
                '  if (a <= 0.001) discard;',
                '  gl_FragColor = vec4(uColor, a);',
                '}'
            ].join('\n'),
            transparent: true, blending: THREE.AdditiveBlending, depthWrite: false
        });
        var flow = new THREE.Mesh(geo, flowMat);
        flow.position.y = 0.02;
        terrainGroup.add(flow);

        // 网格叠加（低分辨率线框，模拟全息扫描网格）
        var wireGeo = new THREE.PlaneGeometry(BOARD * 2, BOARD * 2, 40, 40);
        wireGeo.rotateX(-Math.PI / 2);
        var wp = wireGeo.attributes.position;
        for (var w = 0; w < wp.count; w++) wp.setY(w, terrainH(wp.getX(w), wp.getZ(w)) + 0.012);
        var wireMat = new THREE.MeshBasicMaterial({
            color: 0x5fe4ff, wireframe: true, transparent: true, opacity: 0.055,
            blending: THREE.AdditiveBlending, depthWrite: false
        });
        tintMats.push({ mat: wireMat, k: 1.0 });
        terrainGroup.add(new THREE.Mesh(wireGeo, wireMat));

        // 底座
        var baseMat = new THREE.MeshBasicMaterial({ color: 0x04131d, transparent: true, opacity: 0.95 });
        tintMats.push({ mat: baseMat, k: 0.07 });
        var base = new THREE.Mesh(
            new THREE.BoxGeometry(BOARD * 2 + 0.7, 0.62, BOARD * 2 + 0.7),
            baseMat);
        base.position.y = -0.72;
        terrainGroup.add(base);

        var edgeMat = new THREE.LineBasicMaterial({ color: 0x49d8ff, transparent: true, opacity: 0.55 });
        tintMats.push({ mat: edgeMat, k: 0.9 });
        var edges = new THREE.LineSegments(
            new THREE.EdgesGeometry(new THREE.BoxGeometry(BOARD * 2 + 0.7, 0.62, BOARD * 2 + 0.7)),
            edgeMat);
        edges.position.y = -0.72;
        terrainGroup.add(edges);

        // 底座刻度网格
        var gpts = [];
        for (var g = -BOARD; g <= BOARD; g += 1) {
            gpts.push(g, -0.40, -BOARD, g, -0.40, BOARD);
            gpts.push(-BOARD, -0.40, g, BOARD, -0.40, g);
        }
        var gg = new THREE.BufferGeometry();
        gg.setAttribute('position', new THREE.Float32BufferAttribute(gpts, 3));
        var tickMat = new THREE.LineBasicMaterial({ color: 0x2ea8c8, transparent: true, opacity: 0.16 });
        tintMats.push({ mat: tickMat, k: 0.6 });
        terrainGroup.add(new THREE.LineSegments(gg, tickMat));

        // 四角支架
        var bracketMat = new THREE.LineBasicMaterial({ color: 0x49d8ff, transparent: true, opacity: 0.5 });
        tintMats.push({ mat: bracketMat, k: 0.9 });
        var corners = [[-1, -1], [1, -1], [-1, 1], [1, 1]];
        corners.forEach(function (c) {
            var bracket = [];
            var cx = c[0] * (BOARD + 0.55), cz = c[1] * (BOARD + 0.55);
            bracket.push(cx, -0.30, cz, cx, -0.95, cz);
            bracket.push(cx, -0.95, cz, cx - c[0] * 0.6, -0.95, cz);
            bracket.push(cx, -0.95, cz, cx, -0.95, cz - c[1] * 0.6);
            var bg2 = new THREE.BufferGeometry();
            bg2.setAttribute('position', new THREE.Float32BufferAttribute(bracket, 3));
            terrainGroup.add(new THREE.Line(bg2, bracketMat));
        });
    }

    /* 文字精灵（阵营名 / 战役名）：画布按文字宽度自适应，避免长标题被截断 */
    function makeTextSprite(text, color, baseW, h, fontSize) {
        var t = text || '';
        var font = 'bold ' + (fontSize || 54) + 'px "Microsoft YaHei", sans-serif';

        var measureCv = document.createElement('canvas');
        var measureCtx = measureCv.getContext('2d');
        measureCtx.font = font;
        var textW = Math.max(64, measureCtx.measureText(t).width);
        var pad = 36;
        var cw = Math.min(2048, Math.ceil(textW + pad * 2));

        var cv = document.createElement('canvas');
        cv.width = cw; cv.height = 128;
        var ctx = cv.getContext('2d');
        ctx.clearRect(0, 0, cw, 128);
        ctx.font = font;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.shadowColor = color || '#6fe6ff';
        ctx.shadowBlur = 16;
        ctx.fillStyle = '#eaffff';
        ctx.fillText(t, cw / 2, 66);

        var tex = new THREE.CanvasTexture(cv);
        tex.minFilter = THREE.LinearFilter;
        var sprite = new THREE.Sprite(new THREE.SpriteMaterial({
            map: tex, transparent: true, opacity: 0.92, depthWrite: false, depthTest: false,
            blending: THREE.AdditiveBlending
        }));
        sprite.renderOrder = 30;
        // 世界宽度随画布宽度等比增长，保持字号观感一致
        sprite.scale.set((baseW || 2.6) * (cw / 512), h || 0.65, 1);
        return sprite;
    }

    /* ── 战役旗标 ── */
    function buildMarkers() {
        markerGroup = new THREE.Group();
        scene.add(markerGroup);

        var GR = 0.6180339887;
        factions.forEach(function (f, i) {
            var color = new THREE.Color(f.color || '#38e1ff');
            var list = f.missions || [];
            var total = Math.max(list.length, 1);
            var phase = i * 1.7; // 不同阵营在完整沙盘上使用不同的错落布局

            list.forEach(function (m, k) {
                // 按任务顺序铺满整个沙盘：黄金角螺旋 + 轻微扰动，避免规则网格感
                var t = (k + 0.5) / total;
                var ang = k * GR * Math.PI * 2 + phase + Math.sin(k * 2.3 + i) * 0.12;
                var rad = 1.9 + 4.3 * Math.sqrt(t) + Math.cos(k * 1.7 + i) * 0.18;
                // 收敛到沙盘内侧，避免点位落到边缘低地/底盘外
                rad = clamp(rad, 1.6, 6.2);

                var x = Math.cos(ang) * rad;
                var z = Math.sin(ang) * rad;
                var y = terrainH(x, z);

                var group = new THREE.Group();
                group.position.set(x, y, z);
                group.userData = { id: m.id, faction: i };

                var dim = m.enabled === false;

                // 旗杆
                var poleMat = new THREE.MeshBasicMaterial({
                    color: color, transparent: true, opacity: dim ? 0.25 : 0.6,
                    blending: THREE.AdditiveBlending, depthWrite: false, depthTest: false
                });
                var pole = new THREE.Mesh(new THREE.CylinderGeometry(0.022, 0.03, 0.85, 5), poleMat);
                pole.position.y = 0.43;
                group.add(pole);

                // 旗标头
                var headMat = new THREE.MeshBasicMaterial({
                    color: color, transparent: true, opacity: dim ? 0.3 : 0.98,
                    blending: THREE.AdditiveBlending, depthWrite: false, depthTest: false
                });
                var head = new THREE.Mesh(new THREE.OctahedronGeometry(0.2, 0), headMat);
                head.position.y = 0.95;
                group.add(head);

                // 地面投影环
                var ringMat = new THREE.MeshBasicMaterial({
                    color: color, transparent: true, opacity: dim ? 0.16 : 0.55,
                    side: THREE.DoubleSide, blending: THREE.AdditiveBlending, depthWrite: false, depthTest: false
                });
                var ring = new THREE.Mesh(new THREE.RingGeometry(0.24, 0.32, 24), ringMat);
                ring.rotation.x = -Math.PI / 2;
                ring.position.y = 0.03;
                group.add(ring);

                // 光柱
                var beamMat = new THREE.MeshBasicMaterial({
                    color: color, transparent: true, opacity: dim ? 0.05 : 0.16,
                    blending: THREE.AdditiveBlending, depthWrite: false, depthTest: false,
                    side: THREE.DoubleSide
                });
                var beam = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.012, 0.95, 6, 1, true), beamMat);
                beam.position.y = 0.48;
                group.add(beam);

                // 拾取体（透明球，扩大点击范围）
                var hitMat = new THREE.MeshBasicMaterial({ transparent: true, opacity: 0, depthWrite: false });
                var hit = new THREE.Mesh(new THREE.SphereGeometry(0.45, 8, 8), hitMat);
                hit.position.y = 0.66;
                group.add(hit);

                group.userData.head = head;
                group.userData.ring = ring;
                group.userData.hit = hit;
                group.userData.parts = [headMat, ringMat, beamMat, poleMat];
                group.userData.baseY = y;

                // 点位上方显示战役名
                var mlabel = makeTextSprite(m.name, f.color || '#38e1ff', 2.8, 0.56, 46);
                mlabel.position.y = 1.6;
                group.add(mlabel);
                group.userData.label = mlabel;

                // 旗标与文字始终绘制在地形之上，避免被山脊遮挡显得“掉到地形下面”
                group.traverse(function (o) { o.renderOrder = 20; });
                mlabel.renderOrder = 30;

                markerGroup.add(group);
                markers.push(group);
            });
        });
    }

    function updateCamera(dt) {
        if (intro < 1) {
            intro = Math.min(1, intro + dt * 0.55);
            var e = 1 - Math.pow(1 - intro, 3);
            radius = 20 - 4.5 * e;
            polar = 1.20 - 0.16 * e;
        }

        var sp = Math.sin(polar) * radius;
        camera.position.set(Math.cos(azimuth) * sp, Math.cos(polar) * radius + 2.4, Math.sin(azimuth) * sp);
        camera.lookAt(0, 0.55, 0);
    }

    function animate() {
        if (disposed) return;
        raf = requestAnimationFrame(animate);
        elapsed += 0.016;
        if (flowMat && !reduced) flowMat.uniforms.uTime.value = elapsed;
        updateCamera(0.016);

        if (!reduced) {
            for (var i = 0; i < markers.length; i++) {
                var mk = markers[i];
                if (!mk.visible) continue;
                var isSel = mk.userData.id === selectedId;
                var isHover = mk.userData.id === hoverId;
                var pulse = 1 + (isSel ? 0.5 : (isHover ? 0.28 : 0.10)) * Math.sin(elapsed * 2.6 + i * 0.7);
                mk.userData.head.scale.setScalar(pulse);
                mk.userData.ring.rotation.z += isSel ? 0.05 : 0.012;
                if (isSel) {
                    mk.position.y = mk.userData.baseY + 0.14 + 0.03 * Math.sin(elapsed * 2.2);
                } else {
                    mk.position.y = mk.userData.baseY;
                }
            }
        }

        renderer.render(scene, camera);
        drawLink();
    }

    /* ── 选中点 → 信息框的 2D 引线 ── */
    function drawLink() {
        if (!linkCtx || !linkCanvas) return;
        var w = linkCanvas.width, h = linkCanvas.height;
        linkCtx.clearRect(0, 0, w, h);
        if (selectedId === null || !cardEl) return;

        var mk = null;
        for (var i = 0; i < markers.length; i++)
            if (markers[i].userData.id === selectedId) { mk = markers[i]; break; }
        if (!mk) return;

        var rect = stage.getBoundingClientRect();
        var cardRect = cardEl.getBoundingClientRect();
        var p = new THREE.Vector3();
        mk.userData.head.getWorldPosition(p);
        p.project(camera);
        if (p.z > 1) return;

        var dpr = linkCanvas.width / Math.max(rect.width, 1);
        var mx = (p.x * 0.5 + 0.5) * rect.width * dpr;
        var my = (-p.y * 0.5 + 0.5) * rect.height * dpr;
        var cx = (cardRect.left - rect.left) * dpr;
        var cy = (cardRect.top - rect.top + 34) * dpr;
        if (cx < 0) cx = 0;

        var midX = (mx + cx) / 2;
        linkCtx.save();
        linkCtx.strokeStyle = accentStroke;
        linkCtx.lineWidth = 1.4 * dpr;
        linkCtx.shadowColor = accentShadow;
        linkCtx.shadowBlur = 10 * dpr;
        linkCtx.beginPath();
        linkCtx.moveTo(mx, my);
        linkCtx.lineTo(midX, my);
        linkCtx.lineTo(cx, cy);
        linkCtx.stroke();

        linkCtx.fillStyle = accentDot;
        linkCtx.beginPath();
        linkCtx.arc(mx, my, 3.2 * dpr, 0, Math.PI * 2);
        linkCtx.fill();
        linkCtx.restore();
    }

    function resize() {
        if (!renderer || !canvas) return;
        var w = Math.max(stage.clientWidth, 1);
        var h = Math.max(stage.clientHeight, 1);
        renderer.setSize(w, h, false);
        camera.aspect = w / h;
        camera.updateProjectionMatrix();

        if (linkCanvas && linkCtx) {
            var dpr = Math.min(window.devicePixelRatio || 1, 2);
            linkCanvas.width = Math.round(w * dpr);
            linkCanvas.height = Math.round(h * dpr);
            linkCanvas.style.width = w + 'px';
            linkCanvas.style.height = h + 'px';
        }
    }

    function pickMarker(ev) {
        var rect = canvas.getBoundingClientRect();
        pointer.x = ((ev.clientX - rect.left) / rect.width) * 2 - 1;
        pointer.y = -((ev.clientY - rect.top) / rect.height) * 2 + 1;
        raycaster.setFromCamera(pointer, camera);
        var hits = raycaster.intersectObjects(markerGroup.children, true);
        for (var i = 0; i < hits.length; i++) {
            var o = hits[i].object;
            while (o && (!o.userData || o.userData.id === undefined)) o = o.parent;
            // 阵营之外的隐藏点位不可点击
            if (o && o.visible !== false && o.userData && o.userData.id !== undefined) return o.userData.id;
        }
        return null;
    }

    function onPointerDown(e) {
        dragging = true; moved = false; lastX = e.clientX; lastY = e.clientY;
        if (canvas.setPointerCapture) { try { canvas.setPointerCapture(e.pointerId); } catch (x) { } }
    }

    function onPointerMove(e) {
        if (dragging) {
            var dx = e.clientX - lastX, dy = e.clientY - lastY;
            if (Math.abs(dx) + Math.abs(dy) > 2) moved = true;
            lastX = e.clientX; lastY = e.clientY;
            azimuth -= dx * 0.005;
            polar = clamp(polar - dy * 0.004, 0.42, 1.35);
            return;
        }
        var id = pickMarker(e);
        hoverId = id;
        canvas.style.cursor = id !== null ? 'pointer' : 'grab';
    }

    function onPointerUp(e) {
        if (!dragging) return;
        dragging = false;
        if (!moved) {
            var id = pickMarker(e);
            if (id !== null && dotnet) dotnet.invokeMethodAsync('OnMarker', id);
        }
    }

    function onWheel(e) {
        e.preventDefault();
        radius = clamp(radius + e.deltaY * 0.012, 9, 24);
    }

    function bind() {
        canvas.addEventListener('pointerdown', onPointerDown);
        window.addEventListener('pointermove', onPointerMove);
        window.addEventListener('pointerup', onPointerUp);
        canvas.addEventListener('wheel', onWheel, { passive: false });
        canvas.addEventListener('contextmenu', function (e) { e.preventDefault(); });
    }

    function unbind() {
        if (!canvas) return;
        canvas.removeEventListener('pointerdown', onPointerDown);
        window.removeEventListener('pointermove', onPointerMove);
        window.removeEventListener('pointerup', onPointerUp);
        canvas.removeEventListener('wheel', onWheel);
    }

    /* 阵营主色联动：地形顶点色、流动光带、网格/底座/支架线、引线统一换色 */
    function cssRgba(c, a) {
        return 'rgba(' + Math.round(c.r * 255) + ',' + Math.round(c.g * 255) + ',' + Math.round(c.b * 255) + ',' + a + ')';
    }

    function applyAccent(fc) {
        if (terrainGeo && vertT) {
            var col = terrainGeo.attributes.color;
            var arr = col.array;
            for (var i = 0; i < vertT.length; i++) {
                var t = vertT[i];
                var base = palette(t, fc);
                var band = Math.floor(t * 9) / 9;
                var bc = palette(band, fc);
                arr[i * 3] = base[0] * 0.55 + bc[0] * 0.45;
                arr[i * 3 + 1] = base[1] * 0.55 + bc[1] * 0.45;
                arr[i * 3 + 2] = base[2] * 0.55 + bc[2] * 0.45;
            }
            col.needsUpdate = true;
            if (terrainMesh) terrainMesh.material.color.setRGB(1, 1, 1);
        }

        for (var m = 0; m < tintMats.length; m++)
            tintMats[m].mat.color.copy(fc).multiplyScalar(tintMats[m].k);

        if (flowMat) flowMat.uniforms.uColor.value.copy(fc);

        accentStroke = cssRgba(fc, 0.75);
        accentShadow = cssRgba(fc, 0.9);
        accentDot = cssRgba(fc.clone().lerp(new THREE.Color(1, 1, 1), 0.45), 0.95);
    }

    function applyFaction() {
        for (var i = 0; i < markers.length; i++) {
            var mk = markers[i];
            var on = mk.userData.faction === activeFaction;
            // 沙盘只显示当前阵营的战役点位
            mk.visible = on;
            if (!on) continue;

            var base = mk.userData.id === selectedId ? 1 : 1;
            for (var p = 0; p < mk.userData.parts.length; p++)
                mk.userData.parts[p].opacity = (p === 0 ? 0.98 : p === 1 ? 0.55 : p === 2 ? 0.16 : 0.6) * base;
            mk.scale.setScalar(1.1);
        }

        var fc = new THREE.Color((factions[activeFaction] || {}).color || '#38e1ff');
        applyAccent(fc);
    }

    function mount(cv, ref, payload, linkCv) {
        if (typeof THREE === 'undefined') {
            window.__cwErr = 'THREE undefined';
            return false;
        }
        if (typeof payload === 'string') {
            try { payload = JSON.parse(payload); }
            catch (e) { window.__cwErr = 'payload parse: ' + e.message; return false; }
        }

        dispose();
        disposed = false;
        canvas = cv;
        stage = canvas.parentElement || canvas;
        dotnet = ref;
        factions = payload || [];
        activeFaction = 0; selectedId = null; hoverId = null;
        intro = 0;
        tmpV = new THREE.Vector3();

        try {
            renderer = new THREE.WebGLRenderer({ canvas: canvas, antialias: true, alpha: true });
        } catch (e) {
            renderer = null;
            window.__cwErr = 'renderer: ' + (e && e.message ? e.message : e);
            return false;
        }
        renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
        scene = new THREE.Scene();
        camera = new THREE.PerspectiveCamera(42, 1, 0.1, 160);
        raycaster = new THREE.Raycaster();
        pointer = new THREE.Vector2();

        buildTerrain();
        buildMarkers();
        bind();
        applyFaction();
        if (linkCv) bindOverlay(linkCv);

        ro = new ResizeObserver(resize);
        ro.observe(stage);
        resize();
        animate();
        return true;
    }

    function bindOverlay(cv) {
        linkCanvas = cv;
        linkCtx = cv ? cv.getContext('2d') : null;
        resize();
    }

    function link(cardId) {
        cardEl = cardId ? document.getElementById(cardId) : null;
    }

    function unlink() {
        cardEl = null;
        if (linkCtx && linkCanvas) linkCtx.clearRect(0, 0, linkCanvas.width, linkCanvas.height);
    }

    function setFaction(index) {
        if (index < 0 || index >= factions.length) return;
        activeFaction = index;
        applyFaction();
    }

    function select(id) {
        selectedId = id;
        applyFaction();
    }

    function clearSelection() {
        selectedId = null;
        unlink();
        applyFaction();
    }

    function scrollToItem(id) {
        var el = document.getElementById('cw-cs-mi-' + id);
        if (el) el.scrollIntoView({ block: 'nearest', behavior: reduced ? 'auto' : 'smooth' });
    }

    function dispose() {
        disposed = true;
        if (raf) { cancelAnimationFrame(raf); raf = 0; }
        unbind();
        unlink();
        if (linkCtx) { try { linkCtx.clearRect(0, 0, linkCanvas.width, linkCanvas.height); } catch (e) { } }
        if (ro) { try { ro.disconnect(); } catch (e) { } ro = null; }
        disposeObject(terrainGroup); disposeObject(markerGroup);
        if (renderer) { try { renderer.dispose(); } catch (e) { } }
        terrainGroup = markerGroup = null;
        terrainMesh = flowMat = null;
        terrainGeo = null; vertT = null; tintMats = [];
        scene = camera = renderer = null;
        markers = [];
        hoverId = null; selectedId = null; dragging = false;
    }

    window.cwCs = {
        mount: mount,
        bindOverlay: bindOverlay,
        link: link,
        unlink: unlink,
        setFaction: setFaction,
        select: select,
        clearSelection: clearSelection,
        scrollToItem: scrollToItem,
        dispose: dispose,
        resize: resize
    };
})();
