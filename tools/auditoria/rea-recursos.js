// Reconstroi a saida JSONL externalizada do REA e extrai a arvore de recursos PE.
// uso: node rea-recursos.js <artifact.jsonl> <saida.txt>
const fs = require('fs');

const [, , artifactPath, outPath] = process.argv;
if (!artifactPath || !outPath) {
  console.error('uso: node rea-recursos.js <artifact.jsonl> <saida.txt>');
  process.exit(2);
}

const raw = fs.readFileSync(artifactPath, 'utf8');
const chunks = [];
for (const line of raw.split('\n')) {
  if (!line.trim()) continue;
  let row;
  try { row = JSON.parse(line); } catch (e) { continue; }
  if (row.type === 'chunk') chunks.push({ index: row.index, text: row.text });
}
chunks.sort((a, b) => a.index - b.index);
const json = chunks.map(c => c.text).join('');

let obj;
try {
  obj = JSON.parse(json);
} catch (e) {
  // a saida pode repetir o mesmo JSON (resultado + "MCP structured content").
  // extrai apenas o primeiro documento JSON completo.
  const end = firstJsonEnd(json);
  if (end < 0) {
    console.error('falha no parse do JSON reconstruido: ' + e.message);
    console.error('tamanho reconstruido: ' + json.length);
    process.exit(1);
  }
  const primeiro = json.slice(0, end);
  try {
    obj = JSON.parse(primeiro);
  } catch (e2) {
    console.error('falha no parse do primeiro documento: ' + e2.message);
    process.exit(1);
  }
  console.log('usado apenas o primeiro documento JSON (' + end + ' de ' + json.length + ' chars)');
}

function firstJsonEnd(s) {
  let depth = 0, inStr = false, esc = false;
  for (let i = 0; i < s.length; i++) {
    const ch = s[i];
    if (inStr) {
      if (esc) { esc = false; continue; }
      if (ch === '\\') { esc = true; continue; }
      if (ch === '"') inStr = false;
      continue;
    }
    if (ch === '"') { inStr = true; continue; }
    if (ch === '{' || ch === '[') depth++;
    else if (ch === '}' || ch === ']') {
      depth--;
      if (depth === 0) return i + 1;
    }
  }
  return -1;
}

const out = [];
out.push('# evidencia: ' + obj.evidence_id);
out.push('# artefato: ' + obj.subject.name + '  sha256=' + (obj.subject.digest || {}).sha256);
out.push('# limitacoes: ' + ((obj.limitations || []).join(' | ')));
out.push('');

const nr = obj.normalized_result || {};
const out2 = [];
function nm(x) {
  if (x === null || x === undefined) return '';
  if (typeof x === 'object') return String(x.name !== undefined ? x.name : (x.id !== undefined ? x.id : JSON.stringify(x)));
  return String(x);
}
const resources = nr.resources || [];
out.push('# entradas de recurso: ' + resources.length);
for (const r of resources) {
  const t = nm(r.type), n = nm(r.name), l = nm(r.language);
  const payload = r.payload || {};
  out.push(t + '/' + n + '/' + l + '\t' + (payload.size !== undefined ? payload.size + ' bytes' : '') + '\t' + (payload.sha256 || ''));
}
const icons = nr.icon_groups || [];
out.push('');
out.push('# grupos de icone: ' + icons.length);
for (const g of icons) out.push('  recurso ' + g.resource_index + ' -> ' + (g.images || []).length + ' imagens');

fs.writeFileSync(outPath, out.join('\r\n'), 'utf8');
console.log('gravado: ' + outPath + ' (' + out.length + ' linhas)');
console.log('chunks concatenados: ' + chunks.length + ', json: ' + json.length + ' chars');
console.log('recursos: ' + resources.length);
