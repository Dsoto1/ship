# Proposal: letting crew go, more room aboard, passengers who bring their own

**Owner's ask** (playtest, 2026-10-23): "when recruiting i should be able to manage the crew (let some go at cost of
respect or something) in case i forget to find the right person (check list says i need a navigator but i already
filled my ship). Can we add more to the ship capacity? maybe it allows us to potentially hire more but at cost of
resources and everything else. Is there a cabin we can bring extra settlers? maybe they have to bring their own
food/water supplies to help?"

**Status:** a proposal, 2026-11-01. Nothing is built until the owner chooses (questions at the end).

## Where things stand

- **Berths:** the Garza has 7, and she needs 4 hands to sail (`data/port/bocana.json`). Twelve people in La Bocana
  are looking for a berth. Their asking prices add up to about 9,950 sueldos, against a purse of 7,000. One of them,
  asking −400, is a passenger who pays for his passage, but he takes a berth like a hand.
- **No undo:** once someone signs on, there is no way to let them go. With the berths full and no one aboard who
  knows the way, the only choice is to sail without a navigator.
- **Stores** are counted in days for everyone aboard. Each extra mouth already makes the stores run out sooner, so
  more people aboard has a cost without any new rule.

## 1. Letting someone go ("Pay off")

- **What:** on a signed-on hand's card in port, a quiet **Pay off** button, which asks to be confirmed. They take
  their sea chest off the Garza, go back to the port, and their berth is free again.
- **Not at sea.** Later, the Aguada stop could offer "put ashore here".
- **What it costs** (recommended):
  - **The advance:** they keep it. It's spent, and their family has it.
  - **Respect:** everyone else aboard thinks a little less of the captain (−5 respect). Their friends and kin aboard
    think a good deal less (−10 respect, −5 trust). Turning off someone the crew likes is worse than someone they
    don't.
  - **The person paid off:** −20 trust. They won't sign on again on this voyage, and they say so.
  - **Word in port:** for a day, someone who hears of it may ask a little more ("a captain who turns men off").
    Optional; it makes paying off a choice with a real cost rather than a free undo.
- **Alternatives:**
  - **A:** they hand back half the advance, but the respect cost is doubled.
  - **B:** no respect cost, but the advance is lost and a fixed "turning-off" fee is paid (100 sueldos).
- **Work:** small. A simulation command, a few relationship changes, a chronicle line, the button, tests. The save
  format stays as it is (the existing "gone from the port" list is reused).

## 2. More room aboard

- **What the history allows:** a caravel like the Garza often sailed with 20 or more. Seven is a gameplay limit.
  The aim is to let you take more, at a cost.
- **Recommended: hammocks slung in the hold.** The chandler sells room for more hands: up to 3 extra berths (10 in
  all), at 150 sueldos each for hammocks and the carpentry. Each extra berth costs:
  - **Stores:** one more mouth for the passage (already counted).
  - **Crowding:** illness spreads more easily below decks (flux and fever chances rise by a tenth for each extra
    berth used), and morale suffers a little in a storm. Readiness says "Crowded: N in a hold built for 7".
  - **Cargo:** the hold space they take is space the stores need. Each extra berth takes one lot of the stores
    allowance (this needs a simple hold limit, which the game doesn't have yet).
- **Alternatives:**
  - **A:** simply raise the berths to 9, with no new rules (the smallest change).
  - **B:** extra berths only for those who take the hardest watches, at the same pay (no cost but crowding).
- **Work:** medium. A chandler "goods" line, berths that can grow, the crowding effects, a readiness line, tests.
  The hold limit is the larger piece; it can wait if the owner prefers.

## 3. Passengers who bring their own (settlers for the colony)

- **What:** places in the hold for passengers apart from the crew's berths: up to 4, beyond the hands. Passengers
  don't stand watches or work the ship.
  - **Ashore:** they become settlers. They count for the charter's settlement, and they can build, farm and form
    households like the relief ship's settlers.
- **Their own stores:** each passenger (or family) brings their own food and water for the passage, say 30 days,
  kept apart in the stores and marked as theirs.
  - **If the voyage runs long:** once theirs is gone, they eat the ship's, which makes a choice and a tension: share
    or not.
  - **Ashore:** what they have left goes into the camp's stores.
- **They pay passage:** a fee into the purse, which helps pay for the crew. This is the existing −400 offer, kept
  apart from the berths.
- **Who:** three or four new people in La Bocana, each with a reason to go:
  - a young couple with a child, wanting land of their own;
  - a widow who keeps bees and wants to start again;
  - a friar for the chapel ashore;
  - a merchant's factor who wants first sight of the trade.
  
  Each has a conversation, terms, and things that make them a risk as well as a help: the child, the factor's
  loyalties, the friar's quarrels with the captain.
- **Their costs:** they're more mouths if their stores run out, more people to keep well (passengers fall sick like
  anyone), and sometimes trouble (a passenger who complains, or who sides with a grievance).
- **Work:** the largest. New people (models, portraits, conversations), stores kept apart, the passenger places, and
  their arrival ashore as settlers. That last part reuses the relief settlers' path.

## Recommended order

1. **Pay off**, first: it fixes the problem in the playtest (berths full, no navigator) and is small.
2. **More room**, with hammocks and crowding; the hold limit can come later.
3. **Passengers who bring their own**, once 1 and 2 are played: it needs new people written and built.

## Questions for the owner

1. **Paying someone off costs:**
   - **(a) recommended:** they keep the advance, and the crew thinks less of you;
   - **(b):** half the advance back, a larger respect cost;
   - **(c):** a fixed fee, no respect cost.
2. **Word in port after a pay-off** (others ask a little more for a day):
   - **(a) recommended:** yes;
   - **(b):** no.
3. **More berths:**
   - **(a) recommended:** hammocks in the hold, up to 3 extra, with crowding;
   - **(b):** simply raise to 9;
   - **(c):** keep 7.
4. **A hold limit** (berths and stores compete for space):
   - **(a):** now, with the hammocks;
   - **(b) recommended:** later;
   - **(c):** never.
5. **Passengers:**
   - **(a) recommended:** yes, 3 or 4 new people who bring their own stores and become settlers ashore;
   - **(b):** only the existing passenger, kept apart from the berths;
   - **(c):** not now.
6. **Anything to change** in who the passengers are (the family, the beekeeper widow, the friar, the factor)?
