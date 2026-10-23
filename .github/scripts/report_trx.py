"""Print failed tests from .trx files as GitHub Actions error annotations.

Annotations are visible through the GitHub API (check-run annotations), so failures can be diagnosed
without access to the raw job log.
"""
import glob
import os
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def escape(text: str) -> str:
    return text.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")


def main(folder: str) -> int:
    failed = 0
    for path in glob.glob(os.path.join(folder, "**", "*.trx"), recursive=True):
        root = ET.parse(path).getroot()
        for result in root.iterfind(".//t:UnitTestResult", NS):
            if result.get("outcome") != "Failed":
                continue
            failed += 1
            name = result.get("testName", "?")
            message = result.findtext(".//t:Message", default="", namespaces=NS).strip()
            stack = result.findtext(".//t:StackTrace", default="", namespaces=NS).strip()
            detail = (message + "\n" + "\n".join(stack.splitlines()[:5]))[:2000]
            print(f"::error title=Failed test: {escape(name)}::{escape(detail)}")
    print(f"{failed} failed test(s) reported.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else "TestResults"))
