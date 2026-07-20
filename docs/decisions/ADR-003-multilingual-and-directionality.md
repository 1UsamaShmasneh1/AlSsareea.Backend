# ADR-003: Multilingual support and directionality

- Status: Accepted
- Date: 2026-07-20

## Decision

Support Arabic (`ar`), Hebrew (`he`), and English (`en`), with Arabic as the default. The API selects a supported culture from `Accept-Language`; unsupported cultures fall back to Arabic.

The backend owns culture selection and localized content concerns. Client applications own RTL/LTR layout and visual direction. Translated text must be stored or managed through deliberate localization mechanisms rather than scattered as hard-coded strings.
