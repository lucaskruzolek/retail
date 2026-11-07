const fs = require('fs');
const path = require('path');

function parseMarkdown(md) {
    const lines = md.split(/\r?\n/);
    let html = [];
    let inCodeBlock = false;
    let codeBlockContent = [];
    let inTable = false;
    let tableHeaders = [];
    let tableRows = [];
    let inUl = false;
    let inOl = false;

    function closeLists() {
        if (inUl) {
            html.push('</ul>');
            inUl = false;
        }
        if (inOl) {
            html.push('</ol>');
            inOl = false;
        }
    }

    function closeTable() {
        if (!inTable) return;
        let tHtml = '<table><thead><tr>';
        for (const th of tableHeaders) {
            tHtml += `<th>${inlineFormat(th)}</th>`;
        }
        tHtml += '</tr></thead><tbody>';
        for (const row of tableRows) {
            tHtml += '<tr>';
            for (const cell of row) {
                tHtml += `<td>${inlineFormat(cell)}</td>`;
            }
            tHtml += '</tr>';
        }
        tHtml += '</tbody></table>';
        html.push(tHtml);
        inTable = false;
        tableHeaders = [];
        tableRows = [];
    }

    function inlineFormat(text) {
        if (!text) return '';
        // Keycaps like `[F1]` or `[Ctrl + B]`
        text = text.replace(/`\[([^\]]+)\]`/g, '<kbd class="keycap">$1</kbd>');
        text = text.replace(/`([^`]+)`/g, '<code>$1</code>');
        text = text.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
        text = text.replace(/\*([^*]+)\*/g, '<em>$1</em>');
        text = text.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2">$1</a>');
        // MathKaTeX display/inline simplified
        text = text.replace(/\$\$([^$]+)\$\$/g, '<div class="formula">$1</div>');
        text = text.replace(/\$([^$]+)\$/g, '<span class="formula">$1</span>');
        return text;
    }

    for (let i = 0; i < lines.length; i++) {
        let line = lines[i];

        // Code block toggle
        if (line.trim().startsWith('```')) {
            if (inCodeBlock) {
                html.push('<pre><code>' + codeBlockContent.join('\n').replace(/</g, '&lt;').replace(/>/g, '&gt;') + '</code></pre>');
                codeBlockContent = [];
                inCodeBlock = false;
            } else {
                closeLists();
                closeTable();
                inCodeBlock = true;
                codeBlockContent = [];
            }
            continue;
        }

        if (inCodeBlock) {
            codeBlockContent.push(line);
            continue;
        }

        // Horizontal rule
        if (/^---+$/.test(line.trim())) {
            closeLists();
            closeTable();
            html.push('<hr />');
            continue;
        }

        // Table
        if (line.trim().startsWith('|') && line.trim().endsWith('|')) {
            closeLists();
            const cells = line.trim().slice(1, -1).split('|').map(c => c.trim());
            if (!inTable) {
                inTable = true;
                tableHeaders = cells;
            } else if (cells.every(c => /^:?-+:?$/.test(c))) {
                // Divider line, skip
            } else {
                tableRows.push(cells);
            }
            continue;
        } else if (inTable) {
            closeTable();
        }

        // Headings
        const hMatch = line.match(/^(#{1,6})\s+(.*)$/);
        if (hMatch) {
            closeLists();
            const level = hMatch[1].length;
            const content = inlineFormat(hMatch[2]);
            const id = hMatch[2].toLowerCase().replace(/[^\w\s-]/g, '').replace(/\s+/g, '-');
            html.push(`<h${level} id="${id}">${content}</h${level}>`);
            continue;
        }

        // Unordered list
        const ulMatch = line.match(/^(\s*)[*+-]\s+(.*)$/);
        if (ulMatch) {
            if (inOl) { html.push('</ol>'); inOl = false; }
            if (!inUl) { html.push('<ul>'); inUl = true; }
            html.push(`<li>${inlineFormat(ulMatch[2])}</li>`);
            continue;
        }

        // Ordered list
        const olMatch = line.match(/^(\s*)\d+\.\s+(.*)$/);
        if (olMatch) {
            if (inUl) { html.push('</ul>'); inUl = false; }
            if (!inOl) { html.push('<ol>'); inOl = true; }
            html.push(`<li>${inlineFormat(olMatch[2])}</li>`);
            continue;
        }

        closeLists();

        // Empty line
        if (!line.trim()) {
            continue;
        }

        // Blockquote
        if (line.startsWith('>')) {
            html.push(`<blockquote>${inlineFormat(line.replace(/^>\s*/, ''))}</blockquote>`);
            continue;
        }

        // Normal paragraph
        html.push(`<p>${inlineFormat(line)}</p>`);
    }

    closeLists();
    closeTable();

    return html.join('\n');
}

const mdPath = path.resolve(__dirname, '../docs/MANUAL_DE_USUARIO.md');
const outHtmlPath = path.resolve(__dirname, '../docs/MANUAL_DE_USUARIO.html');

const mdContent = fs.readFileSync(mdPath, 'utf8');
const bodyHtml = parseMarkdown(mdContent);

const fullHtml = `<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="UTF-8">
    <title>Retail POS - Manual de Usuario</title>
    <style>
        @page {
            size: A4;
            margin: 20mm 15mm 20mm 15mm;
        }
        body {
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
            font-size: 13px;
            line-height: 1.6;
            color: #18181B;
            background-color: #FFFFFF;
            margin: 0;
            padding: 20px;
        }
        h1 {
            color: #9D0F33;
            font-size: 26px;
            border-bottom: 2px solid #9D0F33;
            padding-bottom: 8px;
            margin-top: 0;
            margin-bottom: 12px;
            page-break-after: avoid;
        }
        h2 {
            color: #9D0F33;
            font-size: 18px;
            border-bottom: 1px solid #E4E4E7;
            padding-bottom: 6px;
            margin-top: 28px;
            margin-bottom: 10px;
            page-break-after: avoid;
        }
        h3 {
            color: #27272A;
            font-size: 14px;
            margin-top: 20px;
            margin-bottom: 8px;
            page-break-after: avoid;
        }
        p {
            margin: 0 0 10px 0;
            text-align: justify;
        }
        ul, ol {
            margin: 0 0 12px 0;
            padding-left: 24px;
        }
        li {
            margin-bottom: 4px;
        }
        table {
            width: 100%;
            border-collapse: collapse;
            margin: 14px 0 18px 0;
            font-size: 12px;
            page-break-inside: avoid;
        }
        th, td {
            border: 1px solid #D4D4D8;
            padding: 6px 10px;
            text-align: left;
        }
        th {
            background-color: #F4F4F5;
            color: #18181B;
            font-weight: 600;
        }
        tr:nth-child(even) {
            background-color: #FAFAFA;
        }
        pre {
            background-color: #18181B;
            color: #F4F4F5;
            padding: 12px 14px;
            border-radius: 6px;
            font-family: "Cascadia Code", Consolas, Monaco, monospace;
            font-size: 11px;
            line-height: 1.4;
            overflow-x: auto;
            page-break-inside: avoid;
            margin: 12px 0;
        }
        code {
            font-family: "Cascadia Code", Consolas, monospace;
            background-color: #F4F4F5;
            color: #9D0F33;
            padding: 2px 5px;
            border-radius: 4px;
            font-size: 11px;
        }
        pre code {
            background-color: transparent;
            color: inherit;
            padding: 0;
        }
        .keycap {
            display: inline-block;
            background-color: #27272A;
            color: #FAFAFA;
            border: 1px solid #3F3F46;
            border-radius: 4px;
            padding: 2px 7px;
            font-family: "Cascadia Code", Consolas, monospace;
            font-size: 11px;
            font-weight: 600;
            box-shadow: 0 1px 1px rgba(0,0,0,0.15);
        }
        .formula {
            font-family: "Cascadia Code", Consolas, monospace;
            color: #9D0F33;
            font-weight: bold;
        }
        div.formula {
            background: #FFF1F4;
            border-left: 3px solid #9D0F33;
            padding: 8px 12px;
            margin: 10px 0;
            border-radius: 0 4px 4px 0;
        }
        hr {
            border: 0;
            border-top: 1px solid #E4E4E7;
            margin: 24px 0;
        }
        a {
            color: #9D0F33;
            text-decoration: none;
        }
        a:hover {
            text-decoration: underline;
        }
        blockquote {
            border-left: 4px solid #9D0F33;
            background-color: #FFF5F7;
            margin: 12px 0;
            padding: 8px 14px;
            color: #4A0516;
            border-radius: 0 6px 6px 0;
        }
    </style>
</head>
<body>
${bodyHtml}
</body>
</html>`;

fs.writeFileSync(outHtmlPath, fullHtml, 'utf8');
console.log('HTML generado con éxito en: ' + outHtmlPath);

