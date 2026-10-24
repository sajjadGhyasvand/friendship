/* Exercise the real exporter without Chrome. This checks control flow and layout bands,
   not real browser font shaping, emoji support or pixel-level visual correctness. */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../FriendShip.web/wwwroot/friendship/story-card.js'), 'utf8');
const story = {
    Kind: 'personality', OwnerName: 'نام نمایشی برای آزمون خروجی', Title: 'قهرمانِ مرحله‌های خلوت',
    Description: 'یک گوشهٔ دنج و یک بازی خوب؛ برای ماجراجویی همیشه لازم نیست از خونه بزنی بیرون!',
    Emoji: '🎮', VisualKey: 'gaming.controller', PositiveLine: 'سلیقه‌ات امضای خودت رو داره؛ همینش قشنگه ✨',
    Invitation: 'تو هم نسخهٔ غیررسمی خودت رو کشف کن!',
    Facts: Array.from({ length: 4 }, () => ({ Text: 'گاهی بهترین قرارم، خودم و چند ساعت وقت آزاده', Emoji: '🌙', VisualKey: 'social.moon' }))
};
async function run(data, { share = false, font = true, registry = {} } = {}) {
    const drawn = [], downloads = [], requests = [], shares = [], events = {};
    const status = { textContent: '' };
    const button = { dataset: { narrativeStory: 'story', cardUrl: '/Ui' },
        addEventListener: (name, callback) => events[name] = callback,
        hasAttribute: () => share };
    const ctx = new Proxy({
        measureText(text) { return { width: [...text].length * (parseFloat(this.font.match(/\d+px/)?.[0]) || 40) * .55 }; },
        fillText(text, x, y, maxWidth) { drawn.push({ text, x, y, maxWidth }); },
        createLinearGradient() { return { addColorStop() {} }; }
    }, { get(target, key) { return key in target ? target[key] : () => {}; } });
    const canvas = { getContext: () => ctx, toBlob: callback => callback(new Blob(['png'], { type: 'image/png' })) };
    class BrowserURL extends URL { static createObjectURL() { return 'blob:test'; } static revokeObjectURL() {} }
    class File { constructor(blobs, name, options) { this.name = name; this.type = options.type; this.blobs = blobs; } }
    class Image { set src(value) { requests.push(value); this.onerror(); } }
    const document = {
        currentScript: { src: 'https://localhost/friendship/story-card.js' },
        querySelectorAll: selector => selector === '[data-narrative-story]' ? [button] : [],
        querySelector: () => status, getElementById: () => ({ textContent: JSON.stringify(data) }),
        fonts: { load: async () => font ? [{}] : [], ready: Promise.resolve() },
        body: { append() {} },
        createElement: kind => kind === 'canvas' ? canvas : { click() { downloads.push(this.download); }, remove() {} }
    };
    vm.runInNewContext(source, { document, URL: BrowserURL, Image, File, Blob,
        fetch: async () => ({ ok: true, json: async () => registry }),
        navigator: { canShare: () => share, share: async value => shares.push(value) },
        location: { origin: 'https://localhost', href: 'https://localhost/Ui/Discover/Story' }, setTimeout() {} });
    await events.click();
    return { drawn, downloads, shares, status, requests, canvas, button };
}
(async () => {
    const exported = await run(story, { registry: { 'gaming.controller': 'https://outside.example/private.png', 'social.moon': '/images/future-missing.png' } });
    assert.equal(exported.canvas.width, 1080); assert.equal(exported.canvas.height, 1920);
    assert.deepEqual(exported.downloads, ['friendship-story.png']);
    assert(!exported.requests.some(url => url.includes('outside.example')), 'Custom visuals must stay on the local origin');
    assert(exported.drawn.some(t => t.text === '🌙'), 'Missing future images must fall back to emoji');
    assert(exported.drawn.every(t => t.y >= 0 && t.y <= 1900), 'All text baselines must remain inside the story');
    assert.equal(exported.button.disabled, false);
    const shared = await run({ ...story, Facts: story.Facts.slice(0, 2) }, { share: true });
    assert.equal(shared.downloads.length, 0); assert.equal(shared.shares.length, 1);
    assert.equal(shared.shares[0].files[0].type, 'image/png');
    assert.equal(shared.shares[0].url, undefined, 'Image sharing must not include private page URLs');
    const noFont = await run(story, { font: false }); assert.equal(noFont.downloads.length, 0);
    assert(noFont.status.textContent.includes('آماده نشد'));
    const empty = await run({ ...story, Facts: [] }); assert.equal(empty.downloads.length, 0);
    const excessive = await run({ ...story, Facts: [...story.Facts, story.Facts[0]] }); assert.equal(excessive.downloads.length, 0);
    const invalidRegistry = await run(story, { registry: null }); assert.equal(invalidRegistry.downloads.length, 1);
    const invalidAsset = await run(story, { registry: { 'gaming.controller': 'https://' } }); assert.equal(invalidAsset.downloads.length, 1);
    console.log('Story exporter: 9:16 bounds, image/emoji fallback, same-origin registry, file sharing, font failure and empty/excessive content passed.');
})().catch(error => { console.error(error); process.exitCode = 1; });
