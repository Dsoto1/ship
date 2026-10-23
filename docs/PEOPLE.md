# People: who comes, who stays, households, children, the years

**Spec:** ECONOMY_LOOP §32 (households), §39 (population growth as people). This is step P of
ECONOMY_AUDIT.
**Status:** first pass, 2026-10-23. The simulation is in `World/SimulationRunner.People.cs`. The People
drawer has a Households card, and a person's record says how they came and whom they live with.

## Who comes

The company grows by people with names, faces and skills. Nobody arrives as a number.

| Who | When | How many |
|---|---|---|
| **Settlers on the relief ship** | The charter is judged on day 180 | 8 if it is kept, 4 if it is kept in part. They are men and women of Castrena, each with a trade and a second skill. Half of them come as married couples. |
| **The worn out go home** | The charter is revoked | Anyone ashore with stress 60 or more takes passage home on her. The captain stays. |
| **People of the coast** | From day 60, while a people met trusts the camp (45 and over) | About one chance in 60 a day. At most two from each people. They speak their own language well and some Castrenan, and know fishing, foraging and tracking. |
| **A partner** | From day 30, while a people met trusts the camp (50 and over) | About one chance in 80 a day: one of them comes to live with a single member of the company. |
| **A Brennish hand** | The interlopers' ship sails | Half the time, if the camp is Good or Steady: a seaman who would rather stay. |
| **Children** | Born to a household | See below. |

The chronicle records each one: who came, and from where.

## Households

Two single adults of the company (16 and over) who are fond of each other, with affection of 35 or more
each way, make a home together. It happens at about one chance in 30 a day. Each grows fonder of the other.

When one of a people of the coast comes to live with one of the company, the household keeps that people's
customs as well (§32):

- **A gift-giving people (Mahenu, Oruwai):** they expect a gift to the family. The camp gives two bolts of
  trade cloth, or two trade knives if there is no cloth, and trust grows. If there is nothing to give, it is
  talked about: trust falls and a grievance is held.
- **Kessat:** word goes home and trust grows.

## Children and the years

- **Expecting:** a woman of 16 to 42 in a household with a man may expect a child, at about one chance in 120
  a day. The child is due 270 days on.
- **Born:** the child takes a name from their mother's people's names, or a Castrenan name. They have a
  little of their household's languages. Everyone ashore is the lighter for it.
- **Children do no work:** under 12, a child has no duties, cannot be ordered to anything but rest, and
  cannot lead a workplace. Eating and drinking are automatic, so they still eat and drink.
- **Ages:** those born at the camp are as old as the days since their birth. Everyone else is a year older
  each year from the day the ship sailed.
- **The seasons come round:** the rains are 170 days of each 365. The chronicle records when they come and
  when they end.
- **No end at day 180:** play carries on past the relief ship, year after year.

## Saves

Format 44 keeps households, with their couple, children, customs and any child due, and records when and how
each person came. An older company has no households, and everyone in it came with the ship.

## Tests

`PeopleTests` (6):
- settlers when the charter is kept or kept in part, and the worn out going home when it is revoked;
- two who are fond of each other making a home;
- a partner from Mahenu, with gifts to the family;
- children born, grown up at 12, and doing no work before then;
- people of Kessat coming, at most two, and a Brennish hand staying;
- households and children saved and loaded alike.

All 618 tests pass, the day-180 balance among them.

## Not yet

- **Distribution by household:** households and kitchens sharing out food (§40). Everyone still eats from
  the one store.
- **Possessions households share.** (Houses for households are in STRUCTURES.md.)
- **Castaways.**
- **Grief and old age:** a death in a household does not yet weigh on the rest of it, and no one dies of
  old age.
