(() => {
    const container = document.querySelector("[data-sales-indicators]");
    if (!container) return;

    const selects = [...container.querySelectorAll("[data-indicator-select]")];
    const defaultValues = selects.map(select => select.value);
    const preferenceKey = container.dataset.preferenceKey;

    const isValidSelection = values =>
        Array.isArray(values)
        && values.length === selects.length
        && new Set(values).size === values.length
        && values.every(value => [...selects[0].options].some(option => option.value === value));

    const renderValues = () => {
        selects.forEach(select => {
            const card = select.closest("[data-indicator-card]");
            const output = card?.querySelector("[data-indicator-value]");
            const help = card?.querySelector("[data-indicator-help]");
            const selectedOption = select.selectedOptions[0];
            const description = selectedOption?.dataset.description || "Información del indicador.";
            if (output) output.textContent = selectedOption?.dataset.displayValue || "—";
            select.title = description;
            if (help) {
                help.dataset.tooltip = description;
                help.setAttribute("aria-label", `Ayuda: ${description}`);
            }
        });
    };

    const refreshOptions = () => {
        const selectedValues = new Set(selects.map(select => select.value));
        selects.forEach(select => {
            [...select.options].forEach(option => {
                option.disabled = option.value !== select.value && selectedValues.has(option.value);
            });
        });
    };

    const applySelection = values => {
        selects.forEach((select, index) => { select.value = values[index]; });
        renderValues();
        refreshOptions();
    };

    let savedValues = defaultValues;
    try {
        const storedValues = JSON.parse(window.localStorage.getItem(preferenceKey));
        if (isValidSelection(storedValues)) savedValues = storedValues;
    } catch {
        // La configuración predeterminada mantiene operativo el reporte si el almacenamiento no está disponible.
    }
    applySelection(savedValues);

    selects.forEach(select => select.addEventListener("change", () => {
        const values = selects.map(item => item.value);
        if (!isValidSelection(values)) {
            applySelection(savedValues);
            window.showSystemToast?.("warning", "Cada tarjeta debe mostrar un indicador diferente.");
            return;
        }

        applySelection(values);
        try {
            window.localStorage.setItem(preferenceKey, JSON.stringify(values));
            savedValues = values;
            window.showSystemToast?.("success", "La configuración de indicadores se guardó correctamente.");
        } catch {
            window.showSystemToast?.("warning", "El indicador cambió, pero el navegador no permitió guardar la configuración.");
        }
    }));

    const customPeriodForm = document.querySelector("[data-custom-period-form]");
    const fromDate = customPeriodForm?.querySelector("[data-custom-from]");
    const toDate = customPeriodForm?.querySelector("[data-custom-to]");
    const rangeError = customPeriodForm?.querySelector("[data-custom-period-error]");

    const validateCustomRange = () => {
        if (!customPeriodForm || !fromDate || !toDate || !rangeError) return false;

        fromDate.classList.remove("input-validation-error");
        toDate.classList.remove("input-validation-error");
        if (!fromDate.value || !toDate.value) {
            rangeError.textContent = "Seleccione la fecha inicial y la fecha final para actualizar el reporte.";
            rangeError.hidden = false;
            if (!fromDate.value) fromDate.classList.add("input-validation-error");
            if (!toDate.value) toDate.classList.add("input-validation-error");
            return false;
        }

        if (fromDate.value > toDate.value) {
            rangeError.textContent = "La fecha inicial no puede ser posterior a la fecha final.";
            rangeError.hidden = false;
            fromDate.classList.add("input-validation-error");
            toDate.classList.add("input-validation-error");
            return false;
        }

        rangeError.hidden = true;
        rangeError.textContent = "";
        return true;
    };

    if (customPeriodForm) {
        customPeriodForm.addEventListener("submit", event => {
            if (!validateCustomRange()) event.preventDefault();
        });
        [fromDate, toDate].forEach(input => {
            input?.addEventListener("change", () => {
                if (validateCustomRange()) customPeriodForm.requestSubmit();
            });
        });
    }

    const customPeriodFields = document.querySelector("[data-custom-period-fields]");
    const synchronizeCustomPeriodVisibility = () => {
        if (!customPeriodFields) return;
        const activePeriod = new URLSearchParams(window.location.search).get("period") || "month";
        customPeriodFields.hidden = activePeriod !== "custom";
    };

    document.querySelectorAll("[data-period-option]").forEach(option => {
        option.addEventListener("click", () => {
            if (customPeriodFields && option.dataset.periodOption !== "custom") customPeriodFields.hidden = true;
        });
    });
    window.addEventListener("pageshow", synchronizeCustomPeriodVisibility);
    synchronizeCustomPeriodVisibility();

    const historyChart = document.querySelector("[data-sales-history-chart]");
    const historyData = document.querySelector("[data-daily-sales-history]");
    const historyMode = document.querySelector("[data-sales-history-mode]");
    const historyYear = document.querySelector("[data-sales-history-year]");
    const historyMonth = document.querySelector("[data-sales-history-month]");
    const historyYearFilter = document.querySelector("[data-sales-history-year-filter]");
    const historyMonthFilter = document.querySelector("[data-sales-history-month-filter]");
    const historyCustomFilter = document.querySelector("[data-sales-history-custom-filter]");
    const historyFrom = document.querySelector("[data-sales-history-from]");
    const historyTo = document.querySelector("[data-sales-history-to]");
    const historyGroup = document.querySelector("[data-sales-history-group]");
    const historyRangeError = document.querySelector("[data-sales-history-range-error]");

    if (historyChart && historyData && historyMode && historyYear && historyMonth) {
        const monthNames = ["Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"];
        const currentYear = Number(historyChart.dataset.currentYear);
        const currentMonth = Number(historyChart.dataset.currentMonth);
        const currencySymbol = historyChart.dataset.currencySymbol || "Q";
        const currencyDecimals = Number(historyChart.dataset.currencyDecimals || 2);
        const currencyAfter = historyChart.dataset.currencyPosition === "After";
        let dailySales = [];
        try {
            dailySales = JSON.parse(historyData.textContent || "[]").map(item => ({
                year: Number(item.Year),
                month: Number(item.Month),
                day: Number(item.Day),
                total: Number(item.Total)
            }));
        } catch {
            dailySales = [];
        }

        const dailyTotals = new Map(dailySales.map(item => [`${item.year}-${item.month}-${item.day}`, item.total]));
        const monthlyTotals = new Map();
        dailySales.forEach(item => {
            const key = `${item.year}-${item.month}`;
            monthlyTotals.set(key, (monthlyTotals.get(key) || 0) + item.total);
        });
        const availableYears = [...new Set([currentYear, ...dailySales.map(item => item.year)])]
            .filter(Number.isFinite)
            .sort((left, right) => right - left);
        const firstHistoricalYear = Math.min(currentYear, ...availableYears);
        const lastHistoricalYear = Math.max(currentYear, ...availableYears);

        const addOption = (select, value, label) => {
            const option = document.createElement("option");
            option.value = value.toString();
            option.textContent = label;
            select.appendChild(option);
        };

        availableYears.forEach(year => addOption(historyYear, year, year));
        monthNames.forEach((month, index) => addOption(historyMonth, index + 1, month));
        historyYear.value = currentYear.toString();
        historyMonth.value = currentMonth.toString();

        const money = value => {
            const formatted = value.toLocaleString("es-GT", {
                minimumFractionDigits: currencyDecimals,
                maximumFractionDigits: currencyDecimals
            });
            return currencyAfter ? `${formatted} ${currencySymbol}` : `${currencySymbol} ${formatted}`;
        };

        const getMonthlyTotal = (year, month) => monthlyTotals.get(`${year}-${month}`) || 0;
        const getDailyTotal = (year, month, day) => dailyTotals.get(`${year}-${month}-${day}`) || 0;
        const rollingMonths = () => {
            const points = [];
            for (let offset = 11; offset >= 0; offset -= 1) {
                const date = new Date(currentYear, currentMonth - 1 - offset, 1);
                const year = date.getFullYear();
                const month = date.getMonth() + 1;
                points.push({ label: `${monthNames[month - 1]} ${year}`, total: getMonthlyTotal(year, month) });
            }
            return points;
        };

        const yearMonths = year => monthNames.map((label, index) => ({
            label,
            total: getMonthlyTotal(year, index + 1)
        }));

        const comparedMonth = month => {
            const points = [];
            for (let year = firstHistoricalYear; year <= lastHistoricalYear; year += 1) {
                points.push({ label: `${monthNames[month - 1]} ${year}`, total: getMonthlyTotal(year, month) });
            }
            return points;
        };

        const parseDate = value => {
            const parts = value?.split("-").map(Number);
            if (!parts || parts.length !== 3 || parts.some(part => !Number.isFinite(part))) return null;
            return { year: parts[0], month: parts[1], day: parts[2], value: Date.UTC(parts[0], parts[1] - 1, parts[2]) };
        };

        const customRange = () => {
            const from = parseDate(historyFrom?.value);
            const to = parseDate(historyTo?.value);
            if (!from || !to || from.value > to.value) {
                if (historyRangeError) {
                    historyRangeError.textContent = !from || !to
                        ? "Seleccione las fechas Desde y Hasta."
                        : "La fecha Desde no puede ser posterior a Hasta.";
                    historyRangeError.hidden = false;
                }
                return null;
            }
            if (historyRangeError) historyRangeError.hidden = true;

            const points = [];
            if (historyGroup?.value === "months") {
                const cursor = new Date(Date.UTC(from.year, from.month - 1, 1));
                const lastMonth = Date.UTC(to.year, to.month - 1, 1);
                while (cursor.getTime() <= lastMonth) {
                    const year = cursor.getUTCFullYear();
                    const month = cursor.getUTCMonth() + 1;
                    const monthTotal = dailySales
                        .filter(item => item.year === year && item.month === month)
                        .filter(item => {
                            const day = Date.UTC(item.year, item.month - 1, item.day);
                            return day >= from.value && day <= to.value;
                        })
                        .reduce((sum, item) => sum + item.total, 0);
                    points.push({ label: `${monthNames[month - 1]} ${year}`, total: monthTotal });
                    cursor.setUTCMonth(cursor.getUTCMonth() + 1);
                }
                return points;
            }

            const cursor = new Date(from.value);
            while (cursor.getTime() <= to.value) {
                const year = cursor.getUTCFullYear();
                const month = cursor.getUTCMonth() + 1;
                const day = cursor.getUTCDate();
                points.push({
                    label: `${day.toString().padStart(2, "0")}/${month.toString().padStart(2, "0")}`,
                    total: getDailyTotal(year, month, day)
                });
                cursor.setUTCDate(cursor.getUTCDate() + 1);
            }
            return points;
        };

        const updateHistoryControlVisibility = mode => {
            historyYearFilter.hidden = mode !== "year";
            historyMonthFilter.hidden = mode !== "compare";
            if (historyCustomFilter) historyCustomFilter.hidden = mode !== "custom";
        };

        const renderHistory = () => {
            const mode = historyMode.value;
            const selectedYear = Number(historyYear.value || currentYear);
            const selectedMonth = Number(historyMonth.value || currentMonth);
            updateHistoryControlVisibility(mode);
            const points = mode === "year"
                ? yearMonths(selectedYear)
                : mode === "compare"
                    ? comparedMonth(selectedMonth)
                    : mode === "custom"
                        ? customRange()
                        : rollingMonths();
            if (!points) return;
            const maximum = Math.max(0, ...points.map(point => point.total));

            historyChart.replaceChildren();
            points.forEach(point => {
                const column = document.createElement("div");
                column.className = "sales-period-column";
                column.title = `${point.label}: ${money(point.total)}`;

                const value = document.createElement("strong");
                value.className = point.total === 0 ? "sales-period-value is-zero" : "sales-period-value";
                value.textContent = money(point.total);

                const bar = document.createElement("div");
                bar.className = "sales-period-bar";
                bar.style.height = `${maximum === 0 ? 8 : Math.max(8, point.total / maximum * 100)}%`;
                bar.title = `${point.label}: ${money(point.total)}`;

                const label = document.createElement("small");
                label.textContent = point.label;
                column.append(value, bar, label);
                historyChart.appendChild(column);
            });

            historyChart.setAttribute("aria-label", mode === "year"
                ? `Ventas mensuales de ${selectedYear}`
                : mode === "compare"
                    ? `Comparación histórica de ${monthNames[selectedMonth - 1]}`
                    : mode === "custom"
                        ? "Ventas del rango personalizado"
                        : "Ventas de los últimos 12 meses");
        };

        historyMode.addEventListener("change", renderHistory);
        historyYear.addEventListener("change", renderHistory);
        historyMonth.addEventListener("change", renderHistory);
        [historyFrom, historyTo, historyGroup].forEach(control => control?.addEventListener("change", renderHistory));
        renderHistory();
    }
})();
