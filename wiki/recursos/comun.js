// Comportamiento común de la portada y de los recorridos. Se carga al final del <body>, después de recorridos.js
// y de Mermaid. No depende de ningún servidor: funciona abriendo el HTML con doble clic.
(function () {
    "use strict";

    // La raíz de la wiki se deduce de la ubicación de este script (wiki/recursos/comun.js), así ningún documento
    // tiene que declarar a qué profundidad está.
    var raiz = new URL("../", document.currentScript.src);
    var recorridos = window.RETAIL_RECORRIDOS || [];

    function enlace(archivo) {
        return new URL(archivo, raiz).href;
    }

    function crear(etiqueta, clase, texto) {
        var elemento = document.createElement(etiqueta);
        if (clase) { elemento.className = clase; }
        if (texto) { elemento.textContent = texto; }
        return elemento;
    }

    // ------------------------------------------------------------------ Mermaid y modo oscuro
    var consultaOscuro = window.matchMedia("(prefers-color-scheme: dark)");
    if (window.mermaid) {
        // Mermaid fija el tema al dibujar: si cambia el modo del sistema, se recarga para redibujar.
        if (consultaOscuro.matches) { document.documentElement.classList.add("modo-oscuro"); }
        consultaOscuro.addEventListener("change", function () { location.reload(); });
        window.mermaid.initialize({
            startOnLoad: true,
            theme: consultaOscuro.matches ? "dark" : "neutral",
            securityLevel: "strict",
            fontFamily: "Segoe UI, system-ui, sans-serif",
            flowchart: { htmlLabels: true, useMaxWidth: true },
            sequence: { useMaxWidth: false, wrap: true, width: 160, actorMargin: 40, messageMargin: 28, noteMargin: 10, actorFontSize: 13, messageFontSize: 13, noteFontSize: 12 },
            state: { useMaxWidth: true },
            er: { useMaxWidth: true }
        });
    }

    // ------------------------------------------------------------------ Impresión
    // Chrome imprime cerradas las secciones plegadas: se abren antes de imprimir y se restauran después.
    var plegadas = [];
    window.addEventListener("beforeprint", function () {
        plegadas = Array.prototype.filter.call(document.querySelectorAll("details"), function (d) { return !d.open; });
        plegadas.forEach(function (d) { d.open = true; });
    });
    window.addEventListener("afterprint", function () {
        plegadas.forEach(function (d) { d.open = false; });
    });

    // ------------------------------------------------------------------ Destino dentro de un desplegable
    // Un enlace a un título que está dentro de un <details> cerrado no lo abre solo: se abren sus ancestros.
    function abrirDestino() {
        if (!location.hash) { return; }
        var destino = document.getElementById(decodeURIComponent(location.hash.slice(1)));
        var abrio = false;
        for (var nodo = destino; nodo; nodo = nodo.parentElement) {
            if (nodo.tagName === "DETAILS" && !nodo.open) { nodo.open = true; abrio = true; }
        }
        if (abrio) { destino.scrollIntoView(); }
    }
    window.addEventListener("hashchange", abrirDestino);

    // ------------------------------------------------------------------ Recorridos: lateral y anterior/siguiente
    function idDesdeTexto(texto, usados) {
        var base = texto.toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "")
            .replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "") || "seccion";
        var id = base;
        for (var n = 2; usados[id] || document.getElementById(id); n++) { id = base + "-" + n; }
        usados[id] = true;
        return id;
    }

    function armarRecorrido() {
        var principal = document.querySelector("main");
        var titulos = Array.prototype.slice.call(principal.querySelectorAll("h2, h3"));
        var usados = {};
        var actual = new URL(location.href);
        actual.hash = "";

        // Los h3 no tienen id en el HTML: se generan del texto, así el contenido no se toca para poder enlazarlos.
        titulos.forEach(function (titulo) {
            if (!titulo.id) { titulo.id = idDesdeTexto(titulo.textContent, usados); }
        });

        var lateral = crear("nav", "lateral");
        lateral.setAttribute("aria-label", "Secciones del documento");
        lateral.appendChild(crear("div", "rotulo", "En este documento"));
        var lista = crear("ol");
        var itemH2 = null;
        var vinculos = [];
        titulos.forEach(function (titulo) {
            // Los títulos dentro de un desplegable son de preguntas, no secciones: no van a la lateral.
            if (titulo.closest("details")) { return; }
            var item = crear("li");
            var vinculo = crear("a", null, titulo.textContent);
            vinculo.href = "#" + titulo.id;
            item.appendChild(vinculo);
            vinculos.push({ titulo: titulo, vinculo: vinculo, item: item, nivel: titulo.tagName });
            if (titulo.tagName === "H2" || !itemH2) {
                lista.appendChild(item);
                itemH2 = titulo.tagName === "H2" ? item : itemH2;
            } else {
                var sublista = itemH2.querySelector("ol");
                if (!sublista) { sublista = crear("ol"); itemH2.appendChild(sublista); }
                sublista.appendChild(item);
            }
        });
        lateral.appendChild(lista);

        var escritos = recorridos.filter(function (r) { return r.estado === "escrito"; });
        var posicion = -1;
        if (escritos.length) {
            lateral.appendChild(crear("div", "rotulo", "Recorridos"));
            var otros = crear("ol");
            escritos.forEach(function (r, i) {
                var item = crear("li");
                var vinculo = crear("a", null, r.titulo);
                vinculo.href = enlace(r.archivo);
                if (vinculo.href === actual.href) { vinculo.setAttribute("aria-current", "page"); posicion = i; }
                item.appendChild(vinculo);
                otros.appendChild(item);
            });
            lateral.appendChild(otros);
        }
        document.body.insertBefore(lateral, document.body.firstChild);
        document.body.classList.add("con-lateral");

        // Anterior y siguiente, antes del pie del documento.
        if (posicion >= 0 && escritos.length > 1) {
            var paso = crear("nav", "paso");
            paso.setAttribute("aria-label", "Otros recorridos");
            [[posicion - 1, "anterior", "← Anterior"], [posicion + 1, "siguiente", "Siguiente →"]].forEach(function (p) {
                var r = escritos[p[0]];
                if (!r) { return; }
                var vinculo = crear("a", p[1]);
                vinculo.href = enlace(r.archivo);
                vinculo.appendChild(crear("small", null, p[2]));
                vinculo.appendChild(document.createTextNode(r.titulo));
                paso.appendChild(vinculo);
            });
            var pie = principal.querySelector(".pie");
            principal.insertBefore(paso, pie);
        }

        // Sección activa: el último título que ya pasó el borde superior de la ventana.
        var pendiente = false;
        function marcarActiva() {
            pendiente = false;
            var activo = null;
            vinculos.forEach(function (v) {
                if (v.titulo.getBoundingClientRect().top < 120) { activo = v; }
            });
            vinculos.forEach(function (v) {
                v.vinculo.classList.toggle("activo", v === activo);
                v.item.classList.remove("abierta");
            });
            if (activo) {
                // Se despliegan los h3 de la sección que se está leyendo.
                var abierta = activo.nivel === "H2" ? activo.item : activo.item.parentElement.parentElement;
                abierta.classList.add("abierta");
                if (lateral.scrollHeight > lateral.clientHeight) {
                    var caja = activo.vinculo.getBoundingClientRect();
                    if (caja.top < 0 || caja.bottom > window.innerHeight) { activo.vinculo.scrollIntoView({ block: "nearest" }); }
                }
            }
        }
        window.addEventListener("scroll", function () {
            if (!pendiente) { pendiente = true; window.requestAnimationFrame(marcarActiva); }
        }, { passive: true });
        marcarActiva();
    }

    // ------------------------------------------------------------------ Portada: tarjetas y cobertura
    function armarPortada(contenedor) {
        // Los fundamentos explican cómo funciona el sistema, no una funcionalidad: no cuentan en la cobertura.
        var funcionalidades = recorridos.filter(function (r) { return !r.fundamento; });
        var escritos = funcionalidades.filter(function (r) { return r.estado === "escrito"; }).length;
        var cobertura = document.getElementById("cobertura");
        if (cobertura && funcionalidades.length) {
            var pista = crear("div", "pista");
            var relleno = crear("span");
            relleno.style.width = Math.round(100 * escritos / funcionalidades.length) + "%";
            pista.appendChild(relleno);
            cobertura.appendChild(pista);
            cobertura.appendChild(crear("span", null, escritos + " de " + funcionalidades.length + " funcionalidades con su recorrido escrito"));
        }

        var grupoActual = null;
        var grilla = null;
        recorridos.forEach(function (r) {
            if (r.grupo !== grupoActual) {
                grupoActual = r.grupo;
                contenedor.appendChild(crear("h3", "grupo", r.grupo));
                grilla = crear("div", "tarjetas");
                contenedor.appendChild(grilla);
            }
            var escrito = r.estado === "escrito";
            var tarjeta = crear(escrito ? "a" : "div", "tarjeta" + (escrito ? "" : " pendiente"));
            if (escrito) { tarjeta.href = enlace(r.archivo); }
            var titulo = crear("b", null, r.titulo);
            titulo.appendChild(crear("span", "estado" + (escrito ? " escrito" : ""),
                escrito ? "Escrito" : (r.implementado === false ? "Sin código" : "Pendiente")));
            tarjeta.appendChild(titulo);
            tarjeta.appendChild(crear("p", null, r.resumen));
            tarjeta.appendChild(crear("div", "requisitos", r.requisitos.join(" · ")));
            grilla.appendChild(tarjeta);
        });
    }

    var contenedorRecorridos = document.getElementById("lista-recorridos");
    if (contenedorRecorridos) { armarPortada(contenedorRecorridos); }
    if (document.body.classList.contains("recorrido")) { armarRecorrido(); }
    abrirDestino();
})();
