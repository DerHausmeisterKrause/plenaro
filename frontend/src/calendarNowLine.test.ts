import { describe,expect,it } from 'vitest';
import { calendarNowLine } from './calendarNowLine';

const shownDays=[23,24,25,26,27].map(day=>new Date(2026,10,day));

describe('White current-time line in the weekly calendar',()=>{
  it('matches Thursday 10:30 in the current five-day workweek',()=>{
    expect(calendarNowLine(new Date(2026,10,26,10,30,0),shownDays))
      .toEqual({hour:10,dayIndex:3,topPercent:50});
  });
  it('uses the exact minute within its hour and the correct day column',()=>{
    const result=calendarNowLine(new Date(2026,10,23,6,15,0),shownDays);
    expect(result).toEqual({hour:6,dayIndex:0,topPercent:25});
  });
  it('stays hidden outside 06–18 or if today is outside the visible weekdays',()=>{
    expect(calendarNowLine(new Date(2026,10,26,5,59),shownDays)).toBeNull();
    expect(calendarNowLine(new Date(2026,10,26,18,0),shownDays)).toBeNull();
    expect(calendarNowLine(new Date(2026,10,28,12,0),shownDays)).toBeNull();
    expect(calendarNowLine(new Date(2026,11,3,12,0),shownDays)).toBeNull();
  });
});
