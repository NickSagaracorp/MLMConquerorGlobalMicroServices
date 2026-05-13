// MLM-specific ApexCharts helpers, invoked from Blazor via JSInterop.
// All functions are idempotent — safe to call after every data refresh:
// the previous chart instance attached to the element is destroyed first
// so memory does not leak across re-renders or tab switches.
(function (global) {
    'use strict';

    // Map of elementId -> ApexCharts instance for cleanup on re-render.
    var _charts = global._mlmCharts = global._mlmCharts || {};

    function destroyExisting(id) {
        var existing = _charts[id];
        if (existing) {
            try { existing.destroy(); } catch (e) { /* ignore */ }
            delete _charts[id];
        }
    }

    /**
     * Render the Daily Residuals monthly histogram on the given element.
     * categories: ['May 2026', 'Jun 2026', ...] (already localized server-side
     *  or by the caller — JS does no formatting beyond the currency prefix).
     * data: numeric monthly totals aligned with categories.
     */
    global.mlmRenderResidualsChart = function (elementId, categories, data, currency) {
        if (typeof ApexCharts === 'undefined') {
            console.warn('[mlm-charts] ApexCharts not loaded; cannot render', elementId);
            return;
        }
        var el = document.getElementById(elementId);
        if (!el) {
            console.warn('[mlm-charts] element not found:', elementId);
            return;
        }
        destroyExisting(elementId);

        var prefix = currency || '$';
        var options = {
            series: [{ name: 'Earnings', data: data || [] }],
            chart: {
                type: 'bar',
                height: 300,
                toolbar: { show: false },
                fontFamily: 'Inter, Helvetica Neue, Helvetica, Arial, sans-serif'
            },
            colors: ['#00BFA5'],
            plotOptions: {
                bar: { borderRadius: 6, columnWidth: '55%', distributed: false }
            },
            dataLabels: {
                enabled: true,
                formatter: function (val) { return val > 0 ? prefix + Number(val).toFixed(0) : ''; },
                style: { fontSize: '11px', fontWeight: 600, colors: ['#1a202c'] },
                offsetY: -22
            },
            grid: { borderColor: '#f0f2f5', strokeDashArray: 4 },
            xaxis: {
                categories: categories || [],
                axisTicks: { show: false },
                axisBorder: { show: false },
                labels: { style: { colors: '#8695AA', fontSize: '12px' } }
            },
            yaxis: {
                labels: {
                    formatter: function (val) { return prefix + Number(val).toFixed(0); },
                    style: { colors: '#8695AA', fontSize: '12px' }
                }
            },
            tooltip: {
                y: {
                    formatter: function (val) { return prefix + Number(val).toFixed(2); }
                }
            }
        };

        try {
            var chart = new ApexCharts(el, options);
            chart.render();
            _charts[elementId] = chart;
        } catch (e) {
            console.error('[mlm-charts] render failed for', elementId, e);
        }
    };

    /**
     * Total Dual Team Points trend — grouped column chart with three series
     * (Left, Right, Total) over the last N months.
     * categories: ['Jul', 'Aug', ...] (already localized).
     * leftData / rightData / totalData: numeric arrays aligned with categories.
     */
    global.mlmRenderDualTeamTrendChart = function (elementId, categories, leftData, rightData, totalData) {
        if (typeof ApexCharts === 'undefined') {
            console.warn('[mlm-charts] ApexCharts not loaded; cannot render', elementId);
            return;
        }
        var el = document.getElementById(elementId);
        if (!el) return;
        destroyExisting(elementId);

        var options = {
            series: [
                { name: 'Left',  data: leftData  || [] },
                { name: 'Right', data: rightData || [] },
                { name: 'Total', data: totalData || [] }
            ],
            chart: {
                type: 'bar',
                height: 300,
                toolbar: { show: false },
                fontFamily: 'Inter, Helvetica Neue, Helvetica, Arial, sans-serif'
            },
            // Soft-coral / soft-gray / brand-blue — mirrors the UI template.
            colors: ['#F08080', '#A8B0BD', '#3478F6'],
            plotOptions: {
                bar: { borderRadius: 4, columnWidth: '70%' }
            },
            dataLabels: { enabled: false },
            stroke: { show: true, width: 2, colors: ['transparent'] },
            grid: { borderColor: '#f0f2f5', strokeDashArray: 4 },
            xaxis: {
                categories: categories || [],
                axisTicks: { show: false },
                axisBorder: { show: false },
                labels: { style: { colors: '#8695AA', fontSize: '12px' } }
            },
            yaxis: {
                labels: {
                    formatter: function (val) { return Number(val).toLocaleString(); },
                    style: { colors: '#8695AA', fontSize: '12px' }
                }
            },
            legend: { show: false },
            tooltip: {
                y: {
                    formatter: function (val) { return Number(val).toLocaleString() + ' pts'; }
                }
            }
        };

        try {
            var chart = new ApexCharts(el, options);
            chart.render();
            _charts[elementId] = chart;
        } catch (e) {
            console.error('[mlm-charts] DT trend render failed for', elementId, e);
        }
    };

    /** Explicit teardown for OnAfterRenderAsync(false) / Dispose flows. */
    global.mlmDestroyChart = function (elementId) { destroyExisting(elementId); };
})(window);
