"""Summarise runlog.jsonl files pulled by Record-QuestPlaytest.ps1 (stdlib only).

Usage: python Tools/playtest/analyze_runlog.py [file-or-dir ...]   (default: playtests/)
Directories are searched recursively for runlog.jsonl. Only evt == "end" lines are used; identical
lines across files are counted once (each pull copies the device's whole cumulative log).
"""
import json
import sys
from collections import Counter
from pathlib import Path

LOW, HIGH = 0.05, 0.40  # P4-06 target: picked 5-40% of the times it is offered


def load(paths):
    files = []
    for p in map(Path, paths):
        files += sorted(p.rglob("runlog.jsonl")) if p.is_dir() else [p]
    seen, ends = set(), []
    for f in files:
        for line in f.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if not line or line in seen:
                continue
            seen.add(line)
            try:
                e = json.loads(line)
            except json.JSONDecodeError:
                print(f"skipping bad line in {f}", file=sys.stderr)
                continue
            if e.get("evt") == "end":
                ends.append(e)
    return files, ends


def counts(entries, field):
    c = Counter()
    for e in entries:
        for item in e.get(field) or []:
            name, _, n = item.rpartition(":")
            c[name] += int(n)
    return c


def report(ends):
    lines = [f"runs (evt=end): {len(ends)}"]
    lines.append("outcomes: " + ", ".join(f"{k} {v}" for k, v in sorted(Counter(e.get("outcome") or "?" for e in ends).items())))

    offered, picked, screens = Counter(), Counter(), 0
    for e in ends:
        for screen in e.get("boonOffers") or []:
            ids, _, pick = screen.rpartition(">")
            screens += 1
            offered.update(i for i in ids.split(",") if i)
            if pick:
                picked[pick] += 1
    lines.append(f"\nboon pick screens: {screens}")
    lines.append(f"{'boon':32} {'offered':>8} {'picked':>7} {'rate':>7}")
    for boon in sorted(offered, key=lambda b: picked[b] / offered[b]):
        rate = picked[boon] / offered[boon]
        flag = "  < 5%" if rate < LOW else "  > 40%" if rate > HIGH else ""
        lines.append(f"{boon:32} {offered[boon]:>8} {picked[boon]:>7} {rate:>6.1%}{flag}")

    spawns, kills = counts(ends, "spawnCounts"), counts(ends, "killCounts")
    lines.append(f"\n{'archetype':24} {'spawned':>8} {'killed':>7}")
    for a in sorted(spawns.keys() | kills.keys()):
        lines.append(f"{a:24} {spawns[a]:>8} {kills[a]:>7}")

    resisted = sum(e.get("resistedHits", 0) for e in ends)
    weak = sum(e.get("weaknessHits", 0) for e in ends)
    lines.append(f"\nresisted hits: {resisted}   weakness hits: {weak}")
    return "\n".join(lines)


if __name__ == "__main__":
    files, ends = load(sys.argv[1:] or ["playtests"])
    if not files:
        sys.exit("no runlog.jsonl found")
    print(report(ends))
