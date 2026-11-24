No authorization or ownership checks. Any authenticated user can cancel, reschedule or delete any booking. Out of scope for v1 per the spec, and a stretch goal.
Booking check-then-save race. Two simultaneous overlapping requests can both pass the check. This is the one that hurts users most (you worked out why). The fix is a serializable transaction or a concurrency token on the parent resource.
Hard deletes. DELETE on a booking removes it, with a separate cancel endpoint. DELETE on a resource cascades to its bookings. Deactivation is the normal path.
UTC not enforced at the API boundary. Say what a client sending a non-UTC time gets.
No paging on list endpoints.
Business logic lives in controllers. Extraction to IBookingService is planned.
Register reveals whether an email or username is taken (409). Accepted trade-off. The usual fix is a uniform response plus email confirmation.
Concurrent duplicate registration: state what the global handler actually returns, since you tested it.
Token payload is readable by the bearer (sub, email, jti), and the signing key is validated at startup (min 32 bytes).
Masked emails and IPs (POPIA): what you log, and what you deliberately don’t.
Existing bookings on a later-deactivated resource can’t be rescheduled. It’s a deliberate rule.
The “Extentions” typo: just fix the folder and namespace rather than documenting it.
===============
keep in mind
===============
Keep in mind some controllers can fail in multiple ways, 
and the API spec is not always clear on which error code to return. 
For example, a booking reschedule might fail due to a non-existent booking (404) or an invalid time (400). The API should clearly document the expected error codes for each endpoint.