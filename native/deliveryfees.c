#include "deliveryfees.h"

int64_t delivery_reserve_minor(int64_t amount_tiyn, int32_t rate_bp)
{
    if (amount_tiyn < 0 || amount_tiyn > 100000000LL ||
        rate_bp < 0 || rate_bp > 1000)
        return -1;
    // The validated product cannot overflow int64_t.
    // 10000 bp = 100%; add half divisor for positive half-up rounding.
    return (amount_tiyn * rate_bp + 5000LL) / 10000LL;
}
