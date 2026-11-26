import {describe,expect,it} from 'vitest';
import {isoWeekNumber} from './isoWeek';

describe('ISO 8601 week number on the workplace workday card',()=>{
  it('uses the German Monday-start calendar week',()=>{
    expect(isoWeekNumber(new Date(2026,10,26,12))).toBe(48);
    expect(isoWeekNumber(new Date(2026,0,1,12))).toBe(1);
  });
  it('handles year transitions and ISO weeks with 53 weeks',()=>{
    expect(isoWeekNumber(new Date(2021,0,1,12))).toBe(53);
    expect(isoWeekNumber(new Date(2020,11,31,12))).toBe(53);
    expect(isoWeekNumber(new Date(2022,0,1,12))).toBe(52);
    expect(isoWeekNumber(new Date(2027,0,4,12))).toBe(1);
  });
});
