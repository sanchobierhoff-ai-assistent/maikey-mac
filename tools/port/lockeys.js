// Controleert of alle L.T/L.Tf/Tr.Key-sleutels in nl/en/de staan. Loc.T heeft een terugval (alleen NL),
// dus die tellen we apart: ontbrekend in en/de betekent dat Engelstalige gebruikers Nederlands zien.
const fs = require('fs'), path = require('path');
const root = require('path').resolve(__dirname, '../../mAIkey.Desktop');
const files = [];
(function walk(d) { for (const f of fs.readdirSync(d)) { const p = path.join(d, f); if (/\\(bin|obj)$/.test(p) || /\/(bin|obj)$/.test(p)) continue; const st = fs.statSync(p); if (st.isDirectory()) walk(p); else if (/\.(cs|axaml)$/.test(f)) files.push(p); } })(root);
const hard = new Map(), soft = new Map();
for (const f of files) {
  const s = fs.readFileSync(f, 'utf8');
  for (const m of s.matchAll(/\bL\.Tf?\(\s*"([A-Za-z0-9_]+)"/g)) hard.set(m[1], f);
  for (const m of s.matchAll(/Tr\.Key="([A-Za-z0-9_]+)"/g)) hard.set(m[1], f);
  for (const m of s.matchAll(/\bLoc\.Tf?\(\s*"([A-Za-z0-9_]+)"/g)) soft.set(m[1], f);
}
const langs = {};
for (const l of ['nl', 'en', 'de']) {
  const t = fs.readFileSync(`${root}/Resources/Localization/${l}.txt`, 'utf8');
  langs[l] = new Set(t.split(/\r?\n/).map(x => x.split('=')[0].trim()).filter(Boolean));
}
for (const [label, map] of [['HARD (toont [Key])', hard], ['SOFT (terugval NL)', soft]]) {
  for (const l of ['nl', 'en', 'de']) {
    const miss = [...map.keys()].filter(k => !langs[l].has(k)).sort();
    if (miss.length) console.log(`${label} ${l}: ${miss.length}\n  ` + miss.join(' '));
  }
}
