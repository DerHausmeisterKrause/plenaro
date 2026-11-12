using Plenaro.Api.Znuny;

namespace Plenaro.Api.Tasks;

/// <summary>
/// Znuny's per-agent TimeAccountingGet is authoritative for booked minutes.
/// Plenaro's local confirmed journal can represent the SAME booking; reconcile
/// as a multiset of (ticket number, time units), not by adding both totals.
/// </summary>
public static class ReportAccounting
{
    public sealed record LocalBooking(string TicketNumber,string Title,long Seconds);
    public sealed record TicketRow(string TicketNumber,string Title,
        long RemoteSeconds,long LocalOnlySeconds,long TotalSeconds,
        int RemoteCount,int LocalOnlyCount);
    public sealed record MergeResult(IReadOnlyList<TicketRow> Tickets,
        long RemoteSeconds,long LocalOnlySeconds,long TotalSeconds,
        int RemoteEntries,int LocalOnlyEntries);

    public static MergeResult Reconcile(
        IReadOnlyList<ZnunyReader.AccountedEntry> remote,
        IReadOnlyList<LocalBooking> confirmedLocal)
    {
        // No remote row has a stable public ID. Match one journal booking to
        // at most one identical remote entry, even if the ticket and duration
        // happen multiple times in the same month.
        var unpaired=confirmedLocal
            .GroupBy(x=>(x.TicketNumber,x.Seconds))
            .ToDictionary(g=>g.Key,g=>g.Count());
        var counts=new Dictionary<string,(string Title,long Remote,long Unmatched,int RemoteCount,int LocalCount)>(
            StringComparer.Ordinal);
        foreach(var r in remote)
        {
            if(!counts.TryGetValue(r.TicketNumber,out var item))
                item=(r.TicketTitle,0,0,0,0);
            if(string.IsNullOrWhiteSpace(item.Title))item.Title=r.TicketTitle;
            item.Remote+=r.Seconds;item.RemoteCount++;
            counts[r.TicketNumber]=item;
            var key=(r.TicketNumber,r.Seconds);
            if(unpaired.TryGetValue(key,out var pending)&&pending>0)
                unpaired[key]=pending-1;
        }
        foreach(var l in confirmedLocal)
        {
            var key=(l.TicketNumber,l.Seconds);
            if(!unpaired.TryGetValue(key,out var count)||count==0)continue;
            unpaired[key]=count-1;
            if(!counts.TryGetValue(l.TicketNumber,out var item))
                item=(l.Title,0,0,0,0);
            if(string.IsNullOrWhiteSpace(item.Title))item.Title=l.Title;
            item.Unmatched+=l.Seconds;item.LocalCount++;
            counts[l.TicketNumber]=item;
        }
        var tickets=counts.Select(kv=>new TicketRow(kv.Key,kv.Value.Title,
                kv.Value.Remote,kv.Value.Unmatched,
                kv.Value.Remote+kv.Value.Unmatched,
                kv.Value.RemoteCount,kv.Value.LocalCount))
            .OrderByDescending(x=>Math.Abs(x.TotalSeconds))
            .ThenBy(x=>x.TicketNumber,StringComparer.Ordinal)
            .ToArray();
        return new MergeResult(tickets,tickets.Sum(x=>x.RemoteSeconds),
            tickets.Sum(x=>x.LocalOnlySeconds),tickets.Sum(x=>x.TotalSeconds),
            tickets.Sum(x=>x.RemoteCount),tickets.Sum(x=>x.LocalOnlyCount));
    }
}
