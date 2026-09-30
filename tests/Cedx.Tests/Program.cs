using Cedx.Core.Models;
using Cedx.Core.Parsing;
using Cedx.Core.Services;
int checks=0;
void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;}
var parser=new AssetTextParser();
var report="""
Hostname: LAB-01
IP Address: 192.0.2.10
Windows account: LAB\operator
OS Version: Microsoft Windows 11 Pro
AnyDesk ID: 123456789
=== Local account details ===
Name : operator
Enabled : True
Description : a long description
              continued here

Name : service
Enabled : False
=== Office vNext diagnostic (original user) ===
========== vNext licenses found ==========
No licenses found
=== Windows license keys ===
OEM : AAAAA-BBBBB-CCCCC-DDDDD-EEEEE
=== Collector details ===
Warning : Access is denied.
""";
var a=parser.Parse(report,"lab.txt",DateTimeOffset.UtcNow);
Check(a.DetailSections.Any(s=>s.Title=="Local account details"),"Extended section");
var accounts=a.DetailSections.Single(s=>s.Title=="Local account details");
Check(accounts.Rows.Count(r=>r.Field=="Name")==2,"Multiple records");
Check(accounts.Rows.Any(r=>r.Value.Contains("continued here")),"Wrapped value");
Check(a.DetailSections.Single(s=>s.Title.StartsWith("Office vNext")).RawContent.Contains("No licenses found"),"Nested banner");
Check(new AssetQuery{Search="operator \"Access is denied\""}.Matches(a),"AND phrase search");
Check(!new AssetQuery{Search="operator missingterm"}.Matches(a),"AND excludes");
Check(!a.SearchIndex.Contains("AAAAA-BBBBB"),"Keys excluded from search");
Check(AssetDetails.RedactKeys(report).Contains("[key hidden]"),"Key masking");
Check(new AssetQuery().Matches(a),"Unknown measurements included");
Check(!new AssetQuery{IncludeUnknown=false}.Matches(a),"Unknown measurements excluded");
Check(!new AssetQuery{MinRam=10,MaxRam=5}.Matches(a),"Invalid range");
Check(new AssetQuery{MinRam=double.NaN}.Validation.Length>0,"Nonfinite range");
Check(a.HasAnyDesk,"Valid remote ID");
a.Network.AnyDeskId="Not installed";Check(!a.HasAnyDesk,"Invalid remote ID");
a.LocalDisks=[new(){DriveLetter="C:",FreeGb=20}];a.LowStorageThresholdGb=30;Check(a.HasLowStorage,"Configurable threshold");
var notified=false;a.PropertyChanged+=(_,e)=>notified|=e.PropertyName==nameof(a.OnlineStatus);a.OnlineStatus=ScanStatus.Online;Check(notified,"Live status notification");
Check(parser.Parse("Hostname: OLD-PC","old.txt",DateTimeOffset.UtcNow).DetailSections.Count>0,"Legacy sparse report");
if(args.Length>0){var real=parser.Parse(File.ReadAllText(args[0]),args[0],DateTimeOffset.UtcNow);Check(real.DetailSections.Count>30,"Real report sections");Console.WriteLine($"Local report: {real.DetailSections.Count} sections, {real.DetailSections.Sum(s=>s.Rows.Count)} fields");}
Console.WriteLine($"PASS: {checks} checks");

// Integration tests use isolated disposable SQLite files, never a user's database.
var temp=Path.Combine(Path.GetTempPath(),"cedx-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
try{
 var file=Path.Combine(temp,"scan.txt");File.WriteAllText(file,report);File.SetLastWriteTimeUtc(file,DateTime.UtcNow.AddMinutes(-2));
 var store=new Cedx.Core.Storage.InventoryDatabase(Path.Combine(temp,"inventory.db"));
 Check(store.Import([file]).Added==1,"Database import");
 var saved=store.Load().Single();
 var assignment=new Cedx.Core.Storage.AssetAssignment("Sample company","Sample person","Engineering","Room 2","A-001","line one\nline two");
 store.SaveAssignment(saved.AssetId,assignment);
 Check(store.Import([file]).Unchanged==1,"Duplicate scan not duplicated");
 File.AppendAllText(file,"\n=== New section ===\nValue : Added field\n");File.SetLastWriteTimeUtc(file,DateTime.UtcNow);
 Check(store.Import([file]).Updated==1,"Updated scan");
 var reopened=new Cedx.Core.Storage.InventoryDatabase(store.Path).Load().Single();
 Check(reopened.Assignment==assignment&&reopened.IsManaged,"Assignment survives import and reopen");
 Check(reopened.RevisionCount==2,"Scan revisions persisted");
 // Regression: hardware, disk and network fields after a NUL-padded EDID name
 // must survive both current-scan loading and history, even on unchanged import.
 var nulText = "Hostname: LAB-NUL\nWindows account: LAB\\operator\nIP Address: 192.0.2.44\nMonitor Model: LAB DISPLAY\0\0\0\0\nSystem Manufacturer: Lab maker\nSystem Model: Lab board\nSerial Number: Default string\n\nNetwork Configuration:\n    Network Mode: Static (0)\n    DNS Servers: 192.0.2.53\n    Default Gateway: 192.0.2.1\n\nAntivirus:\n  Lab protection\n\n=== Local Disks (Space & Type) ===\n C: Total: 952906 MB, Free: 845510.39 MB, Type: SSD\n";
 var nulFile=Path.Combine(temp,"nul.txt");File.WriteAllText(nulFile,nulText);
 var nulStore=new Cedx.Core.Storage.InventoryDatabase(Path.Combine(temp,"nul.db"));
 Check(nulStore.Import([nulFile]).Added==1,"NUL report imported");
 var nulAsset=nulStore.Load().Single();
 Check(nulAsset.RawContent==nulText,"Complete raw report survives SQLite NUL padding");
 Check(nulAsset.Model=="Lab board"&&nulAsset.Manufacturer=="Lab maker"&&nulAsset.Antivirus=="Lab protection","Fields after monitor survive database round trip");
 Check(nulAsset.Network.DefaultGateway=="192.0.2.1"&&nulAsset.Network.DnsServers=="192.0.2.53"&&Math.Abs(nulAsset.CDriveFreeGb!.Value-845510.39/1024d)<0.001,"Legacy disk and network values survive database round trip");
 nulStore.SaveAssignment(nulAsset.AssetId,assignment);
 Check(nulStore.Import([nulFile]).Unchanged==1&&nulStore.Load().Single().Assignment==assignment,"Existing unchanged scans restore all fields and retain assignment");
 Check(nulStore.History(nulAsset.AssetId).Single().RawText==nulText,"History preserves complete NUL report");
 // Simulate the old desktop writer: stored prefix, full-report hash and saved assignment.
 using(var legacy=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+nulStore.Path)){
  legacy.Open();using var damage=legacy.CreateCommand();
  damage.CommandText="UPDATE assets SET raw=$prefix; UPDATE revisions SET raw=$prefix;";
  damage.Parameters.AddWithValue("$prefix",nulText[..nulText.IndexOf('\0')]);damage.ExecuteNonQuery();
 }
 Check(nulStore.Import([nulFile]).Updated==1,"Unchanged source repairs legacy truncated storage");
 var repaired=nulStore.Load().Single();
 Check(repaired.RawContent==nulText&&repaired.AssetId==nulAsset.AssetId&&repaired.Assignment==assignment&&repaired.RevisionCount==1,"Repair preserves asset identity, assignment and revision count");
 Check(nulStore.History(nulAsset.AssetId).Single().RawText==nulText,"Repair restores matching historical report");
 var nulBackup=Path.Combine(temp,"nul-backup.db");nulStore.Backup(nulBackup);
 Check(new Cedx.Core.Storage.InventoryDatabase(nulBackup).Load().Single().RawContent==nulText,"Backup preserves complete NUL report");
 var utf16=Path.Combine(temp,"unicode.txt");File.WriteAllText(utf16,report.Replace("LAB-01","LAB-UTF16"),System.Text.Encoding.Unicode);
 Check(store.Import([utf16]).Added==1&&store.Load().Any(x=>x.Hostname=="LAB-UTF16"),"UTF16 BOM decoded");
 var invalid=Path.Combine(temp,"notes.txt");File.WriteAllText(invalid,"This is documentation and is not an asset scan.");
 Check(store.Import([invalid]).Errors==1&&store.Load().Count==2,"Reject unrelated TXT with reason");
 var old=Path.Combine(temp,"older.txt");File.WriteAllText(old,report+"\nOlder revision\n");File.SetLastWriteTimeUtc(old,DateTime.UtcNow.AddDays(-10));
 store.Import([old]);Check(store.Load().Single(x=>x.AssetId==saved.AssetId).RawContent.Contains("Added field"),"Older report does not overwrite current");
 var backup=Path.Combine(temp,"backup.db");store.Backup(backup);Check(new Cedx.Core.Storage.InventoryDatabase(backup).Load().Count==2,"Restorable database backup");
 File.Delete(file);Check(new Cedx.Core.Storage.InventoryDatabase(store.Path).Load().Count==2,"Source removal does not remove stored asset");
 var reviewDir=Path.Combine(temp,"review");Directory.CreateDirectory(reviewDir);
 var reviewFile=Path.Combine(reviewDir,"source.txt");File.WriteAllText(reviewFile,report);
 var review=new ScanReviewService();var staged=review.Read(reviewFile);
 Check(review.Scan(reviewDir).Scans.Count==1,"Preview folder discovers scans without import");
 var renamed=review.Rename(staged,"renamed.txt");Check(!File.Exists(reviewFile)&&File.Exists(renamed),"Rename preserves report");
 staged=review.Read(renamed);var backupFile=review.Save(staged,report+"\nUser Email(s): operator@example.invalid\n");
 Check(File.ReadAllText(backupFile)==report&&review.Scan(reviewDir).Scans.Count==1,"Edit keeps exact backup outside TXT inventory");
 bool conflict=false;try{review.Save(staged,report);}catch(IOException){conflict=true;}Check(conflict,"Stale preview cannot overwrite changed scan");
 staged=review.Read(renamed);Check(new AssetQuery{Search="operator@example.invalid"}.Matches(staged.Asset),"Email lookup reaches scan index");
 var removed=review.Remove(staged);Check(review.Scan(reviewDir).Scans.Count==0&&File.Exists(removed.RecoveryPath),"Removed scan excluded but recoverable");
 review.Restore(removed);Check(File.Exists(renamed),"Removed scan can be restored");
 var collision=Path.Combine(reviewDir,"occupied.txt");File.WriteAllText(collision,report);conflict=false;
 try{review.Rename(review.Read(renamed),"occupied.txt");}catch(IOException){conflict=true;}Check(conflict&&File.Exists(renamed),"Rename refuses to overwrite another TXT");
 Console.WriteLine($"PASS: {checks} total parser, filter, persistence and import checks");
}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(temp,true);}
