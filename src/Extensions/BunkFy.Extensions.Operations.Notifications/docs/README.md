# Operations Notifications Extension

This extension converts selected BunkFy product events into already-addressed
GMA notification requests. Source modules own business facts, BunkFy owns
recipient and content policy, Organizations authoritatively filters active
membership, and Notifications owns generic inbox persistence and delivery.

The executable personal-data catalogue is
[personal-data-catalog.v1.json](personal-data-catalog.v1.json). Its deterministic
resolved view is
[personal-data-inventory.v1.md](personal-data-inventory.v1.md).

Payloads are a closed set of typed navigation records. Product source events may
contain richer audit or workflow data, but the inbox receives only the minimal
resource identifiers and dates needed to understand or open the affected item.
