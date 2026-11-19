# Experiments

An experiment answers "would this even work here?", not "should we build
it?" A feature proposal without a real issue goes to design/deferred.md,
not here. An experiment without a decision criterion is a speculative
feature wearing a lab coat.

Open a doc at design/experiments/<NN>-<slug>.md (next free number) with
three things, before writing any code:

1. The question, stated so a yes/no answer exists.
2. The criteria that decide the answer, measurable where possible.
3. The time box: an effort limit (an afternoon, one spike per backend).
   Never money. An experiment ends when its box is spent.

Experiment code lives on an `exp/<slug>` branch and is never merged
as-is. The branch is the evidence; if the answer is yes, shipping code
is written properly under plan/. Outcomes, recorded in the doc:

- Adopted: the question resolved yes. Spawn a plan initiative whose
  phase-01 doc links back to this experiment doc.
- Parked: unresolved or premature. Move the idea to design/deferred.md
  with the reopening condition the experiment revealed.
- Rejected: the question resolved no. Record the evidence. The evidence
  rules out the attempt; it rules out the approach only if it genuinely
  does.

Doc template (copy into <NN>-<slug>.md):

    # exp: <NN>-<slug>
    Question: <stated as a yes/no question>
    Criteria: <what decides the answer>
    Time box: <effort limit>
    Result: <Adopted|Parked|Rejected>: <evidence, one paragraph>

The distinction from the deferred register: the register asks "is there a
real issue worth building this?"; an experiment asks "would this even
work here?" A parked entry may cite an experiment as the reason its
reopening condition is trusted.
