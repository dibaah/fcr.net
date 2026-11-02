"""Quick HTML structural check: balanced div/section tags + no duplicate ids."""
import re
import sys
from html.parser import HTMLParser
from pathlib import Path

VOID = {"area", "base", "br", "col", "embed", "hr", "img", "input", "link",
        "meta", "param", "source", "track", "wbr"}


class Checker(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.stack = []
        self.errors = []
        self.ids = []
        self.classes = {}

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if "id" in attrs:
            self.ids.append(attrs["id"])
        if "class" in attrs:
            for cls in attrs["class"].split():
                self.classes.setdefault(cls, 0)
                self.classes[cls] += 1
        if tag not in VOID:
            self.stack.append((tag, self.getpos()))

    def handle_endtag(self, tag):
        if tag in VOID:
            return
        if not self.stack:
            self.errors.append(f"line {self.getpos()[0]}: stray </{tag}>")
            return
        open_tag, pos = self.stack.pop()
        if open_tag != tag:
            self.errors.append(
                f"line {self.getpos()[0]}: </{tag}> closes <{open_tag}> opened at line {pos[0]}"
            )


path = Path(sys.argv[1] if len(sys.argv) > 1 else "static/index.html")
checker = Checker()
checker.feed(path.read_text(encoding="utf-8"))

for tag, pos in checker.stack:
    checker.errors.append(f"line {pos[0]}: <{tag}> never closed")

dupes = sorted({i for i in checker.ids if checker.ids.count(i) > 1})
if dupes:
    checker.errors.append(f"duplicate ids: {dupes}")

if checker.errors:
    print(f"{path}: {len(checker.errors)} problem(s)")
    for err in checker.errors:
        print("  -", err)
    sys.exit(1)

print(f"{path}: structure OK ({len(checker.ids)} ids, {len(checker.classes)} distinct classes)")
