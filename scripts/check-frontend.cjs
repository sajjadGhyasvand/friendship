/* Structural checks for the responsive stylesheet and the desktop answer regression. */
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
for (const name of ['site.css', 'game-theme.css']) {
    const source = fs.readFileSync(path.join(root, 'FriendShip.web/wwwroot/friendship', name), 'utf8');
    const stack = [];
    let quote = null, comment = false, segment = '', line = 1;
    let globalOptions = 0;
    for (let index = 0; index < source.length; index++) {
        const char = source[index], next = source[index + 1];
        if (char === '\n') line++;
        if (comment) { if (char === '*' && next === '/') { comment = false; index++; } continue; }
        if (quote) { if (char === '\\') { index++; continue; } if (char === quote) quote = null; continue; }
        if (char === '/' && next === '*') { comment = true; index++; continue; }
        if (char === '"' || char === "'") { quote = char; continue; }
        if (char === '{') {
            const selector = segment.trim();
            if ((name === 'site.css' && /^\.live-options$/.test(selector)) ||
                (name === 'game-theme.css' && /^\.fs-app \.game-options$/.test(selector))) {
                if (stack.some(block => block.selector.startsWith('@media')))
                    throw new Error(`${name}:${line}: answer layout accidentally scoped to a media query`);
                globalOptions++;
            }
            stack.push({ selector, line }); segment = '';
        } else if (char === '}') {
            if (!stack.length) throw new Error(`${name}:${line}: unexpected closing brace`);
            stack.pop(); segment = '';
        } else if (char === ';') segment = '';
        else segment += char;
    }
    if (quote || comment || stack.length) throw new Error(`${name}: unclosed CSS block at ${JSON.stringify(stack)}`);
    if (!globalOptions) throw new Error(`${name}: globally available answer layout missing`);
    console.log(`${name}: balanced blocks; answer layout applies at all viewport widths`);
}
for (const name of ['profile-wizard', 'play', 'sharing', 'story-card', 'discovery']) {
    const child = require('node:child_process').spawnSync(process.execPath,
        ['--check', path.join(root, `FriendShip.web/wwwroot/friendship/${name}.js`)], { encoding: 'utf8' });
    if (child.status !== 0) throw new Error(child.stderr);
    console.log(`${name}.js: syntax OK`);
}
