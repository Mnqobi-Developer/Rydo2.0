# Pricing

RYDO calculates fares on the ASP.NET Core backend. Mobile clients display an
authoritative quote and never calculate or submit a fare amount.

## Launch rules

| Category | Rate | Minimum fare |
| --- | ---: | ---: |
| Solo | R8.50/km | R25.00 |
| Group | R13.00/km | R35.00 |
| Group+ | R18.00/km | R50.00 |

The distance component uses the Google driving-route distance:

```text
distance charge = route kilometres × category rate
base fare = max(distance charge, category minimum)
passenger total = base fare + booking fee + demand adjustment
```

Money is rounded to two decimal places using midpoint-away-from-zero rounding.
The launch booking fee is R0 and the launch demand multiplier is 1.0x.

## Quote lifecycle

`POST /api/v1/pricing/quotes` calculates the route once and returns all three
category options with their itemised breakdown. The quote:

- belongs to the authenticated Passenger;
- stores its pricing-rule version and currency;
- expires after five minutes;
- is invalidated if pickup or destination coordinates change;
- can be consumed only once;
- becomes the immutable estimated fare attached to the trip.

Previously created trips are not repriced when configuration changes. Legacy
trip rows created before the pricing migration keep nullable pricing metadata.

## Production extensions

The persisted breakdown already separates minimum-fare adjustment, booking
fee, demand adjustment, estimated tolls, waiting fee, and discount. Tolls and
waiting remain zero at quote time until their operational sources and final
fare policies are enabled. Cancellation charges must be recorded separately
because a cancelled trip has no normal completed-trip fare.

Any future change must use a new `Pricing:Version`; published versions must not
be edited in place. Demand pricing is validation-capped at 1.5x.
