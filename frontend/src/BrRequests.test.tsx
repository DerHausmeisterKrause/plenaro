import {afterEach,beforeEach,describe,expect,it,vi} from 'vitest';
import {cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {DayMarkersPanel} from './DayMarkersPanel';
import {BrRecipientsSettingsPanel} from './BrRecipientsSettingsPanel';

const mocks=vi.hoisted(()=>({
  getMonthMarkers:vi.fn(),getMarkerSummary:vi.fn(),saveDayMarker:vi.fn(),
  getMarkerBudget:vi.fn(),saveMarkerBudget:vi.fn(),
  readBrMailStatus:vi.fn(),sendBrRequest:vi.fn(),preflightHomeofficeMail:vi.fn(),
  readBrRecipients:vi.fn(),saveBrRecipients:vi.fn(),
  graphStatus:vi.fn()
}));
vi.mock('./dayMarkersApi',()=>({
  getMonthMarkers:mocks.getMonthMarkers,getMarkerSummary:mocks.getMarkerSummary,
  saveDayMarker:mocks.saveDayMarker,getMarkerBudget:mocks.getMarkerBudget,
  saveMarkerBudget:mocks.saveMarkerBudget
}));
vi.mock('./graphApi',()=>({
  graphStatus:mocks.graphStatus
}));
vi.mock('./brRequestsApi',()=>({
  GermanDay:(date:string)=>{
    const [year,month,day]=date.split('-');
    return `${day}.${month}.${year}`;
  },
  readBrMailStatus:mocks.readBrMailStatus,sendBrRequest:mocks.sendBrRequest,
  preflightHomeofficeMail:mocks.preflightHomeofficeMail,
  readBrRecipients:mocks.readBrRecipients,saveBrRecipients:mocks.saveBrRecipients
}));

beforeEach(()=>{
  vi.clearAllMocks();
  mocks.getMonthMarkers.mockResolvedValue([]);
  mocks.getMarkerSummary.mockResolvedValue({
    year:2026,month:11,annualUlDays:null,usedUlDays:0,
    remainingUlDays:null,ulExhausted:false,ulExceeded:false,
    monthlyBrDays:null,usedBrDays:0,remainingBrDays:null,
    brExhausted:false,brExceeded:false
  });
  mocks.saveDayMarker.mockResolvedValue(undefined);
  mocks.readBrMailStatus.mockResolvedValue({status:'none'});
  mocks.preflightHomeofficeMail.mockResolvedValue({ready:true});
  mocks.sendBrRequest.mockResolvedValue({status:'accepted',
    message:'Microsoft Graph hat den Versand angenommen.'});
  mocks.readBrRecipients.mockResolvedValue({
    recipient1:'team@example.org',recipient2:'leitung@example.org'
  });
  mocks.saveBrRecipients.mockResolvedValue(undefined);
  mocks.graphStatus.mockResolvedValue({
    enabled:true,linked:true,vaultReady:true,connected:true
  });
});
afterEach(cleanup);

describe('BR marker confirmation and mail delivery',()=>{
  it('cancelling the question saves no BR marker and sends no email',async()=>{
    render(<DayMarkersPanel initialDay="2026-11-16"/>);
    const marker=await screen.findByLabelText('BR-Tag markieren');
    fireEvent.click(marker);
    fireEvent.click(screen.getByRole('button',{name:'Marker speichern'}));
    expect(screen.getByRole('dialog').textContent).toContain(
      'Willst du wirklich am 16.11.2026 Homeoffice beantragen?');
    fireEvent.click(screen.getByRole('button',{name:'Nein'}));
    expect(mocks.saveDayMarker).not.toHaveBeenCalled();
    expect(mocks.sendBrRequest).not.toHaveBeenCalled();
  });

  it('saves BR and sends once only after the user explicitly confirms',async()=>{
    const refreshed=vi.fn();
    render(<DayMarkersPanel initialDay="2026-11-16" onSaved={refreshed}/>);
    fireEvent.click(await screen.findByLabelText('BR-Tag markieren'));
    fireEvent.click(screen.getByRole('button',{name:'Marker speichern'}));
    expect(mocks.sendBrRequest).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button',{name:'Ja, beantragen'}));
    await waitFor(()=>expect(mocks.preflightHomeofficeMail).toHaveBeenCalledTimes(1));
    await waitFor(()=>expect(mocks.saveDayMarker).toHaveBeenCalledWith(
      '2026-11-16','Normal',true,false));
    await waitFor(()=>expect(mocks.sendBrRequest).toHaveBeenCalledWith('2026-11-16'));
    expect(refreshed).toHaveBeenCalledTimes(1);
  });

  it('triggers the requested yes/no confirmation for a newly marked HO day',async()=>{
    render(<DayMarkersPanel initialDay="2026-11-16"/>);
    fireEvent.click(await screen.findByLabelText('HO – Homeoffice'));
    fireEvent.click(screen.getByRole('button',{name:'Marker speichern'}));
    expect(screen.getByRole('dialog').textContent).toContain(
      'Willst du wirklich am 16.11.2026 Homeoffice beantragen?');
    fireEvent.click(screen.getByRole('button',{name:'Ja, beantragen'}));
    await waitFor(()=>expect(mocks.saveDayMarker).toHaveBeenCalledWith(
      '2026-11-16','Normal',false,true));
    await waitFor(()=>expect(mocks.sendBrRequest).toHaveBeenCalledWith('2026-11-16'));
  });

  it('preflights recipients and Mail.Send before any HO marker is saved',async()=>{
    mocks.preflightHomeofficeMail.mockRejectedValue(
      new Error('Outlook Mail.Send wurde noch nicht freigegeben.'));
    render(<DayMarkersPanel initialDay="2026-11-16"/>);
    fireEvent.click(await screen.findByLabelText('HO – Homeoffice'));
    fireEvent.click(screen.getByRole('button',{name:'Marker speichern'}));
    fireEvent.click(screen.getByRole('button',{name:'Ja, beantragen'}));
    expect(await screen.findByText(/Homeoffice wurde nicht beantragt/)).toBeTruthy();
    expect(mocks.preflightHomeofficeMail).toHaveBeenCalledTimes(1);
    expect(mocks.saveDayMarker).not.toHaveBeenCalled();
    expect(mocks.sendBrRequest).not.toHaveBeenCalled();
  });

  it('does not send a new HO request when the user chooses No',async()=>{
    vi.stubGlobal('confirm',vi.fn(()=>false));
    render(<DayMarkersPanel initialDay="2026-11-16"/>);
    fireEvent.click(await screen.findByLabelText('HO – Homeoffice'));
    fireEvent.click(screen.getByRole('button',{name:'Marker speichern'}));
    fireEvent.click(screen.getByRole('button',{name:'Nein'}));
    expect(mocks.saveDayMarker).not.toHaveBeenCalled();
    expect(mocks.sendBrRequest).not.toHaveBeenCalled();
  });

  it('never resends when editing an existing BR marker',async()=>{
    mocks.getMonthMarkers.mockResolvedValue([{
      day:'2026-11-16',dayType:'Normal',isBr:true,isHo:false
    }]);
    mocks.readBrMailStatus.mockResolvedValue({status:'accepted'});
    render(<DayMarkersPanel initialDay="2026-11-16"/>);
    await waitFor(()=>expect(screen.getByLabelText('BR-Tag markieren')
      .getAttribute('type')).toBe('checkbox'));
    fireEvent.click(screen.getByRole('button',{name:'Marker speichern'}));
    await waitFor(()=>expect(mocks.saveDayMarker).toHaveBeenCalledTimes(1));
    expect(mocks.sendBrRequest).not.toHaveBeenCalled();
  });

  it('a persisted BR marker without mail can be sent only after fresh confirmation',async()=>{
    mocks.getMonthMarkers.mockResolvedValue([{
      day:'2026-11-16',dayType:'Normal',isBr:true,isHo:false
    }]);
    render(<DayMarkersPanel initialDay="2026-11-16"/>);
    const trigger=await screen.findByRole('button',{
      name:'Homeoffice-Antrag per E-Mail senden'
    });
    fireEvent.click(trigger);
    expect(screen.getByRole('dialog')).toBeTruthy();
    expect(mocks.sendBrRequest).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button',{name:'Ja, beantragen'}));
    await waitFor(()=>expect(mocks.sendBrRequest).toHaveBeenCalledTimes(1));
  });

  it('shows unknown mail status rather than offering an automatic retry',async()=>{
    mocks.getMonthMarkers.mockResolvedValue([{
      day:'2026-11-16',dayType:'Normal',isBr:true,isHo:false
    }]);
    mocks.readBrMailStatus.mockResolvedValue({status:'unknown'});
    render(<DayMarkersPanel initialDay="2026-11-16"/>);
    expect(await screen.findByText(/Homeoffice-Mailversandstatus nicht bestätigt/)).toBeTruthy();
    expect(screen.queryByRole('button',{
      name:'Homeoffice-Antrag per E-Mail senden'
    })).toBeNull();
  });
});

describe('BR mail recipients belong to each personal account',()=>{
  it('does not display a repeated connection button when M365 is linked',async()=>{
    render(<BrRecipientsSettingsPanel/>);
    await screen.findByLabelText('E-Mail-Empfänger 1');
    expect(await screen.findByText(/Microsoft 365 ist verbunden/)).toBeTruthy();
    expect(screen.queryByText('Einmalig Outlook verbinden')).toBeNull();
    expect(mocks.graphStatus).toHaveBeenCalledTimes(1);
  });
  it('shows one-time connect only if no saved Microsoft token exists',async()=>{
    mocks.graphStatus.mockResolvedValue({
      enabled:true,linked:true,vaultReady:true,connected:false
    });
    render(<BrRecipientsSettingsPanel/>);
    await screen.findByLabelText('E-Mail-Empfänger 1');
    expect(await screen.findByText('Einmalig Outlook verbinden')).toBeTruthy();
  });

  it('loads and saves both addresses under personal settings',async()=>{
    render(<BrRecipientsSettingsPanel/>);
    const first=await screen.findByLabelText('E-Mail-Empfänger 1');
    expect((first as HTMLInputElement).value).toBe('team@example.org');
    fireEvent.change(screen.getByLabelText('E-Mail-Empfänger 2 (optional)'),{
      target:{value:'vertretung@example.org'}
    });
    fireEvent.click(screen.getByRole('button',{name:'Empfänger speichern'}));
    await waitFor(()=>expect(mocks.saveBrRecipients).toHaveBeenCalledWith({
      recipient1:'team@example.org',recipient2:'vertretung@example.org'
    }));
  });
});
