#ifndef DELIVERYFEES_H
#define DELIVERYFEES_H
#include <stdint.h>
#if defined(_WIN32)
#  define DELIVERY_API __declspec(dllexport)
#else
#  define DELIVERY_API __attribute__((visibility("default")))
#endif
#ifdef __cplusplus
extern "C" {
#endif
/* Signed 64-bit KZT tiyn (100 tiyn = 1 KZT); integer rate basis points.
   Valid amount: [0,100000000] tiyn (at most 1m KZT).
   Valid rate: [0,1000] basis points (at most 10%).
   Returns rounded reserve tiyn (half-up for nonnegative inputs), or -1 on invalid input.
   Example: 180000 tiyn (1800 KZT) at 200 bp = 3600 tiyn (36 KZT).
   No allocation, global state, pointer, or resource transfer. */
DELIVERY_API int64_t delivery_reserve_minor(int64_t amount_tiyn, int32_t rate_bp);
#ifdef __cplusplus
}
#endif
#endif
