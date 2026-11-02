// Decode a PNG captured from the real UI and compare it with the displayed URL.
// Run with jsqr@1.4.0 and pngjs@7.0.0 in an isolated Docker helper (see verification).
const fs = require('node:fs');
const path = require('node:path');
const { PNG } = require('pngjs');
const jsQR = require('jsqr');
const [imageFile, urlFile] = process.argv.slice(2);
if (!imageFile || !urlFile) throw new Error('Usage: node check-qr-image.cjs <image.png> <expected-url.txt>');
const image = PNG.sync.read(fs.readFileSync(imageFile));
const expected = fs.readFileSync(urlFile, 'utf8').trim();
const decoded = jsQR(new Uint8ClampedArray(image.data), image.width, image.height);
if (!decoded || decoded.data !== expected) throw new Error('QR image does not decode to the exact displayed table URL.');
const result = { result: 'PASS', decodedUrlMatchesDisplayedUrl: true, width: image.width, height: image.height };
fs.writeFileSync(path.join(__dirname, '../docs/verification/qr-image-result.json'), JSON.stringify(result, null, 2) + '\n');
console.log(JSON.stringify(result));
