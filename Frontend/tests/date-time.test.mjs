import test from 'node:test';
import assert from 'node:assert/strict';
import {dateTime} from '../src/lib/date-time.ts';
test('payment time includes the full date and seconds in restaurant timezone',()=>{
  const rendered=dateTime('2026-11-02T17:05:09Z');
  assert.match(rendered,/3\/11\/26/);
  assert.match(rendered,/00:05:09/);
});
test('absent or malformed timestamps do not crash the history view',()=>{
  assert.equal(dateTime(null),'—');assert.equal(dateTime('not-a-date'),'—');
});
