import test from 'node:test';
import assert from 'node:assert/strict';
import { copyTableUrl } from '../src/lib/copy-table-url.ts';

const url = 'http://localhost:3000/qr/' + '1234567890abcdef'.repeat(4);
test('copy sends the full displayed URL without truncating the QR token', async () => {
  const writes = [];
  const result = await copyTableUrl(url, {writeText: async value => { writes.push(value); }}, () => {throw new Error('Unexpected fallback');});
  assert.equal(result, 'copied');
  assert.deepEqual(writes, [url]);
});
test('permission failure selects the URL for manual Ctrl+C', async () => {
  let selected = false;
  const result = await copyTableUrl(url, {writeText: async () => {throw new Error('Permission denied');}}, () => {selected = true;});
  assert.equal(result, 'selected');
  assert.equal(selected, true);
});
test('missing clipboard API still provides manual copy', async () => {
  let selected = false;
  assert.equal(await copyTableUrl(url, undefined, () => {selected = true;}), 'selected');
  assert.equal(selected, true);
});
