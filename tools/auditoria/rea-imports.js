// Extrai a lista de DLLs/funcoes importadas de um artefato JSONL do REA (inspect_pe_imports).
// uso: node rea-imports.js <artifact.jsonl> <saida.txt>
const fs = require('fs');
const [, , inPath, outPath] = process.argv;

const raw = fs.readFileSync(inPath, 'utf8');
const chunks = [];
for (const l of raw.split('\n')) {
  if (!l.trim()) continue;
  try { const r = JSON.parse(l); if (r.type === 'chunk') chunks.push({ i: r.index, t: r.text }); } catch (e) { }
}
chunks.sort((a, b) => a.i - b.i);
const s = chunks.map(x => x.t).join('');

let d = 0, st = false, esc = false, end = -1;
for (let i = 0; i < s.length; i++) {
  const ch = s[i];
  if (st) { if (esc) { esc = false; continue; } if (ch === '\\') { esc = true; continue; } if (ch === '"') st = false; continue; }
  if (ch === '"') { st = true; continue; }
  if (ch === '{' || ch === '[') d++;
  else if (ch === '}' || ch === ']') { d--; if (d === 0) { end = i + 1; break; } }
}
const o = JSON.parse(s.slice(0, end));
const nr = o.normalized_result || {};

const out = [];
out.push('# evidencia: ' + o.evidence_id);
out.push('# artefato: ' + o.subject.name + '  sha256=' + (o.subject.digest || {}).sha256);
out.push('# limitacoes: ' + ((o.limitations || []).join(' | ')));
out.push('');

const dlls = nr.imports || [];
function nm(x) {
  if (x === null || x === undefined) return '';
  if (typeof x === 'object') return String(x.name !== undefined ? x.name : (x.ordinal !== undefined ? 'ord#' + x.ordinal : JSON.stringify(x)));
  return String(x);
}
let total = 0;
for (const d2 of dlls) {
  const fname = nm(d2.name);
  const fns = d2.functions || [];
  const names = fns.map(f => typeof f === 'string' ? f : nm(f.name !== undefined ? f.name : (f.ordinal !== undefined ? 'ord#' + f.ordinal : JSON.stringify(f))));
  total += names.length;
  out.push('=== ' + fname + ' (' + names.length + ') ===');
  out.push(names.join(', '));
}
const delay = nr.delay_imports || [];
out.push('');
out.push('# DLLs com delay-load: ' + delay.length);
const dl = delay.map(d => nm(d.name)).filter(Boolean);
if (dl.length) out.push(dl.join(', '));
out.push('# total de funcoes importadas (tabela normal): ' + total);

fs.writeFileSync(outPath, out.join('\r\n'), 'utf8');
console.log('gravado: ' + outPath + ' | dlls=' + dlls.length + ' funcoes=' + total);
