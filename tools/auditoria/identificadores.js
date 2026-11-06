// Extrai identificadores do dump de strings: metodos .NET, classes, nomes espelhados.
// uso: node identificadores.js <strings.txt> <saida.txt>
const fs = require('fs');

const [, , inPath, outPath] = process.argv;
const lines = fs.readFileSync(inPath, 'utf8').split('\n');
const body = lines.filter(l => l && !l.startsWith('#'));

const sets = {
  namespace: new Set(),
  tipo: new Set(),
  metodo: new Set(),
  lambda: new Set(),
  evento: new Set(),
  propriedade: new Set(),
};

for (const s of body) {
  // namespace com ponto: Namespace.SubNamespace
  if (/^[A-Z][A-Za-z0-9]*(\.[A-Za-z0-9_]+)+$/.test(s) && s.length <= 120) sets.namespace.add(s);
  // <Nome>b__12_0  -> nome real dentro dos colchetes
  const m = /^<([^>]{3,80})>/.exec(s);
  if (m) {
    const inner = m[1];
    if (/^b__|^c__|^d__/.test(inner)) sets.lambda.add(inner);
    else sets.metodo.add(inner);
  }
  // identificador PascalCase isolado
  if (/^[A-Z][a-z][A-Za-z0-9]{3,59}$/.test(s)) {
    if (/(^|_)(get_|set_|_Click|_Changed|_Checked|_Selected|_Pressed|_Toggled|_Loaded|_Closing|_Open|_Close)$/.test(s)) sets.evento.add(s);
    else if (/^(get_|set_)/.test(s)) sets.propriedade.add(s);
    else sets.metodo.add(s);
  }
  // tipo com namespace: Namespace.Type
  const t = /^([A-Z][A-Za-z0-9]*(?:\.[A-Za-z0-9_]+)+)\.(?=[A-Z])/.exec(s);
  if (t) sets.tipo.add(s);
}

const out = [];
out.push('# identificadores extraidos do binario');
out.push('# arquivo: ' + (lines.find(l => l.startsWith('# arquivo:')) || '').replace('# arquivo: ', ''));
for (const k of ['namespace', 'tipo', 'metodo', 'evento', 'propriedade', 'lambda']) {
  const arr = [...sets[k]].sort();
  out.push('');
  out.push('=== ' + k.toUpperCase() + ' (' + arr.length + ') ===');
  out.push(arr.join('\n'));
}
fs.writeFileSync(outPath, out.join('\r\n'), 'utf8');
console.log(inPath.split(/[\\/]/).pop() + ' -> namespace=' + sets.namespace.size + ' tipo=' + sets.tipo.size + ' metodo=' + sets.metodo.size + ' evento=' + sets.evento.size + ' propriedade=' + sets.propriedade.size + ' lambda=' + sets.lambda.size);
