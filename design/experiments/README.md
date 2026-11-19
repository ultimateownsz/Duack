# Experiments

An experiment answers "would this even work here?" — not "should we build
it?" A feature proposal without a real issue goes to design/deferred.md,
not here. An experiment without a decision criterion is a speculative
feature in a lab coat.

Open an experiment doc (exp/<slug>.md) with three things before writing
code:

1. The question, stated so a yes/no answer exists.
2. The criteria that decide the answer (measurable where possible).
3. The time box: an effort limit (an afternoon, one spike per backend) —
   never money. An experiment ends when it is spent.

Code lives on an `exp/<slug>` branch and is never merged as-is. Outcomes:

- Adopted: question resolved yes. Spawn a plan/ initiative; the phase-01
  doc links back to the experiment doc. Shipping code is written properly.
- Parked: unresolved or premature. Move the idea to design/deferred.md
  with the reopening condition the experiment revealed.
- Rejected: resolved no. Record the evidence. Evidence rules out the
  attempt; it rules out the approach only if it genuinely does.

Doc template:

    # exp: <slug>
    Question: ...
    Criteria: ...
    Time box: ...
    Result: <Adopted|Parked|Rejected> — <evidence, one paragraph>
