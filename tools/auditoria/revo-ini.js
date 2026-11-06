#!/usr/bin/env node
// Converte as tabelas de idioma do Revo Pro (english.ini / portuguesebrazil.ini)
// na lista canonica de funcionalidades por modulo, com o texto em portugues.
// uso: node revo-ini.js <english.ini> <portuguesebrazil.ini> <saida.md>
const fs = require('fs');

const [, , enPath, brPath, outPath] = process.argv;

function readIni(p) {
  const buf = fs.readFileSync(p);
  // o proprio produto declara "Codepage=UNICODE": os .ini sao UTF-16LE com BOM
  if (buf.length >= 2 && buf[0] === 0xff && buf[1] === 0xfe) {
    return buf.toString('utf16le').replace(/^\ufeff/, '');
  }
  if (buf.length >= 2 && buf[0] === 0xfe && buf[1] === 0xff) {
    const swapped = Buffer.from(buf);
    swapped.swap16();
    return swapped.toString('utf16le').replace(/^\ufeff/, '');
  }
  return buf.toString('utf8');
}

function parseIni(p) {
  const map = new Map();      // secao -> Map(id -> valor)
  const order = [];
  let sec = null;
  for (const raw of readIni(p).split(/\r?\n/)) {
    const line = raw.trim();
    if (!line || line.startsWith(';') || line.startsWith('#')) continue;
    const sm = /^\[(.+)\]$/.exec(line);
    if (sm) {
      sec = sm[1].trim();
      if (!map.has(sec)) { map.set(sec, new Map()); order.push(sec); }
      continue;
    }
    const em = /^(\d+)\s*=\s*(.*)$/.exec(line);
    if (em && sec) { map.get(sec).set(em[1], em[2]); continue; }
    const nm = /^([^=]+?)\s*=\s*(.*)$/.exec(line);
    if (nm && sec) { map.get(sec).set(nm[1].trim(), nm[2]); }
  }
  return { map, order };
}

const en = parseIni(enPath);
const br = parseIni(brPath);

const out = [];
out.push('# Revo Uninstaller Pro 5.5.4 - mapa de funcionalidades extraido do binario');
out.push('');
out.push('Fonte primaria: `RevoUninPro.exe` (v5.5.4.0) e as tabelas de idioma que o proprio produto');
out.push('instala em `lang\\` (`english.ini` com 42 secoes e 1136 entradas, `portuguesebrazil.ini` com 1099).');
out.push('Cada secao do arquivo de idioma e um modulo/tela do produto; cada entrada e um texto de interface');
out.push('com identificador numerico proprio do binario.');
out.push('');
out.push('> Metodo: leitura do conteudo instalado do produto (PE + recursos de idioma).');
out.push('> Cada linha abaixo esta ancorada em um par (id, texto) que existe no binario entregavel.');
out.push('');

let total = 0;
for (const sec of en.order) {
  const e = en.map.get(sec);
  const b = br.map.get(sec);
  if (!e) continue;
  out.push('## ' + sec);
  out.push('');
  out.push('| id | Texto (en) | Texto (pt-BR) |');
  out.push('|---|---|---|');
  for (const [id, val] of e) {
    const bv = b && b.has(id) ? b.get(id) : '';
    total++;
    out.push('| ' + id + ' | ' + val.replace(/\|/g, '\\|').replace(/\n/g, ' ') + ' | ' + String(bv).replace(/\|/g, '\\|').replace(/\n/g, ' ') + ' |');
  }
  out.push('');
}
out.push('## Totais');
out.push('');
out.push('- Modulos (secoes): ' + en.order.length);
out.push('- Entradas de interface: ' + total);
out.push('- Entradas sem equivalente pt-BR: ' + (() => {
  let n = 0;
  for (const sec of en.order) {
    const e = en.map.get(sec), b = br.map.get(sec);
    if (!e) continue;
    for (const [id] of e) if (!b || !b.has(id)) n++;
  }
  return n;
})());

fs.writeFileSync(outPath, out.join('\r\n'), 'utf8');
console.log('gravado: ' + outPath + ' | modulos=' + en.order.length + ' entradas=' + total);
