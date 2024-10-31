#ifndef PIT_GAUSSIAN
#define PIT_GAUSSIAN

float GaussianDistribution(float x, const float sigma)
{
    return (1.0 / (2.0 * PI * sigma * sigma)) * exp(-x * x / (2.0 * sigma * sigma));
}

#endif