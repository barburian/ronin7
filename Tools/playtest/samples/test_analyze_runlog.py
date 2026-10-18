"""Self-check: python -I Tools/playtest/samples/test_analyze_runlog.py"""
import sys
from pathlib import Path
here = Path(__file__).resolve().parent
sys.path.insert(0, str(here.parent))
import analyze_runlog as a

files, ends = a.load([here, here / "runlog.jsonl"])  # same file twice: deduped
assert len(ends) == 3, ends  # start line and pre-BUG-41 line (no evt) ignored
out = a.report(ends)
rows = {l.split()[0]: l.split()[1:] for l in out.splitlines() if l.strip()}
assert "died 1" in out and "won 1" in out and "abandoned 1" in out, out
assert rows["boon_riposte"] == ["2", "2", "100.0%", ">", "40%"], out
assert rows["boon_keen_edge"] == ["4", "0", "0.0%", "<", "5%"], out
assert rows["Grunt"] == ["34", "31"], out
assert "resisted hits: 6   weakness hits: 10" in out, out
print("ok")
