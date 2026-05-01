const { buildStatus } = require("../src/bounded-fixture");

const failures = [];

expect("collector:safe", buildStatus(" collector ", true), "safe mode status");
expect("unknown:standard", buildStatus("", false), "blank component fallback");

if (failures.length > 0) {
  console.error("bounded-fixture tests failed:");
  for (const failure of failures) {
    console.error(`- ${failure}`);
  }

  process.exit(1);
}

console.log("bounded-fixture tests passed.");

function expect(expected, actual, name) {
  if (actual !== expected) {
    failures.push(`${name}: expected '${expected}', got '${actual}'`);
  }
}
