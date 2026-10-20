-- ============================================================================
-- Semilla dummy robusta y DETERMINISTA del esquema real del HCM
-- (SPEC-012 S2 / tarea T2). Datos 100% ficticios: nombres, teléfonos,
-- correos, direcciones IP, coordenadas y fotos son inventados; no contienen
-- datos personales reales ni secretos, y no representan a personas reales.
--
-- Se ejecuta UNA SOLA VEZ sobre un volumen nuevo, después de schema.sql y
-- antes de las vistas del contrato (init: ON_ERROR_STOP=1). No es idempotente
-- a propósito: re-ejecutarla sobre datos ya cargados falla por PK y no debe
-- hacerse; para regenerar, reinicializar el volumen hcm-postgres.
--
-- REGLA DE DETERMINISMO (la comprueba AC-040 con checksums entre dos
-- reinicializaciones): este fichero NO usa random(), gen_random_uuid() ni
-- now() como dato. Todo procede de generate_series + aritmética sobre el
-- índice + constantes de fecha + CURRENT_DATE. El fichero se ejecuta en una
-- única transacción con TimeZone fijada a UTC, de modo que CURRENT_DATE es la
-- fecha civil UTC del día de la ejecución: dos cargas en el mismo día civil
-- UTC producen filas BYTE-IDÉNTICAS (mismos ids, mismos created_on/modified_on
-- explícitos — ninguna columna depende del DEFAULT CURRENT_TIMESTAMP).
-- Las columnas con semántica de "hoy" (marcaciones de hoy, ausencias que
-- cubren hoy) se derivan de esa fecha UTC: al re-ejecutar en OTRO día civil
-- UTC esas filas cambian de fecha por diseño (la paridad de "hoy" es relativa
-- a la fecha de consulta, igual que en la fuente sintética).
--
-- CONVENCIÓN DE FECHAS DE AUSENCIAS — MUY IMPORTANTE (T3 la aplica):
--   time_end de timeoff es INCLUSIVO (fecha final al estilo formulario,
--   incluye el propio día). El contrato expone un exclusivo ends_before, así
--   que la vista del contrato debe exponer ends_before = date_end + 1 día.
--   Ejemplos de esta semilla (espejo exacto de local_seed.sql):
--     Juan      2026-10-13 → 2026-10-15  (ends_before 2026-10-16)
--     Pedro     2026-10-15 → 2026-10-19  (ends_before 2026-10-20)
--     Mauricio  2026-10-16 → 2026-10-16  (ends_before 2026-10-17)
--
-- VOCABULARIO DE STATUS (verbatim del usuario, 2026-10-20):
--   "1 pendiente, 2 activo, 3 eliminado". Reparto y supuestos por tabla:
--   * employee: 2 activo (≈89%), 1 pendiente (≈5%), 3 eliminado (≈6%, con
--     deleted_on relleno). Los9 empleados de demostración son SIEMPRE 2.
--   * timeoff: 2 = aprobado/activo (es la base de la regla vacation de
--     SPEC-012 §5.1), 1 = pendiente (≈5%), 3 = rechazado/no vigente
--     (≈5%). SUPUESTO: "3" se usa como «Rechazado» para textos del contrato;
--     T3 confirma el mapeo de textos. Ninguna fila 1/3 cuenta como vacation.
--   * time_check: SUPUESTO del generador: 2 = marcación validada (la
--     habitual), 1 = pendiente de revisión, 3 = anulada.
--   * time_check_delay: SUPUESTO: 2 = incidencia aprobada, 1 = pendiente.
--   * working_day / working_day_assigment: 2 = activo, 1 = pendiente
--     (una jornada propuesta), 3 = histórico/eliminado (asignaciones
--     duplicadas antiguas).
--   * timeoff_type: 2 = vigente, 3 = inactivo (el tipo 7 «Formación
--     externa» es el inactivo que pide SPEC-012 §6).
--
-- CENTINELAS (el DDL es NOT NULL sin default): time_check.checkout_type_id
--   = 0 significa «sin evento de salida» (la entrada normal lleva 0; 1 pausa,
--   2 almuerzo, 3 fin de jornada según el usuario). time_check.delay_id = 0
--   significa «sin incidencia»; todo valor ≠ 0 existe en time_check_delay.
--
-- REGLA HORARIA POR OFICINA (simple y documentada): los marcajes se
--   generan en hora local de su oficina y se almacenan como instante UTC
--   (time_check_date_utc) restando laoffset fijo de la ventana actual:
--   Europe/Madrid +02:00 (CEST, toda la ventana de marcajes cae en
--   septiembre-octubre, dentro del horario de verano) y Asia/Tokyo +09:00.
--   Con esa regla la fecha UTC de cada marcaje coincide SIEMPRE con su fecha
--   local (entradas de Madrid 08:00-09:30 → 06:xx UTC; entradas de Tokio
--   09:00-09:30 → 00:xx UTC), de modo que "hoy" es el mismo día en ambas
--   oficinas. Si la ventana de marcajes se ampliara a noviembre, ajustar el
--   desfase de Madrid a -01:00. time_check_date_long es epoch en
--   MILISEGUNDOS UTC (consistente con time_check_date_utc).
--
-- EMPLEADOS DE DEMOSTRACIÓN (paridad con local_seed.sql / SPEC-009-011):
--   ids 1..9 en el mismo orden; name + ' ' + last_name reproduce EXACTAMENTE
--   el display name sintético (p. ej. 'Empleado' + 'sintético 001'); la
--   columna department DOBLA como identificador de unidad orgánica/oficina
--   del contrato (decisión que documenta T3): solo los9 fixtures llevan
--   'office-demo-madrid'/'office-demo-tokyo', los200 generados llevan
--   nombres humanos de departamento (Ingeniería, Ventas…), de modo que la
--   vista del contrato puede acotar el directorio a los fixtures con
--   department LIKE 'office-demo-%' (la suite de paridad exige exactamente 9
--   empleados y 3 ausencias visibles). NO inventa columnas: el shape real no
--   tiene duration/duration_unit en timeoff — T3 reconcilia esos textos.
--
-- PRESENCIA DE HOY (los hechos de paridad que T3 deriva con §5):
--   * Working hoy (entrada check_type_id=1 SIN checkout_type_id=3 hoy):
--     Juan(1), Herrera(4), 001(5), 005(6) — solo marcación de entrada hoy.
--   * Not-arrived hoy (sin NINGÚN marcaje hoy): Mauricio(3), 004(7),
--     Pedro(2), 002(8), 003(9). Los fixtures no tienen histórico de
--     marcaciones: su presencia pasada no es un hecho que el banco verifique.
--   * Vacation hoy: 002(8) y 003(9) reciben AUSENCIAS APROBADAS nuevas que
--     cubren CURRENT_DATE con horizonte rodante de un año (date_start hasta
--     CURRENT_DATE-2, date_end hasta CURRENT_DATE+365; fechas relativas para
--     que "hoy" funcione cualquier día y la paridad no se degrade con el
--     paso de los días sin reinicializar).
--     día). Además10 empleados generados tienen ausencias aprobadas que
--     cubren hoy (alimentan los casos vacation de §5).
--   * DECISIÓN CONOCIDA DE T3 — Pedro: su ausencia fija de paridad termina
--     el 2026-10-19 inclusivo (ends_before 2026-10-20). Cuando hoy es
--     posterior a esa fecha YA NO cubre hoy y su estado derivado sería
--     not-arrived, no vacation (si hoy ≤ 2026-10-19 sí cubre). NO se le
--     inventa una segunda ausencia: la paridad de presencia de Pedro es una
--     decisión de reconciliación de T3 (fixture estático de presencia vs
--     fechas fijas de ausencia) que el padre de tarea elevará al usuario.
--
-- DESVIACIÓN DOCUMENTADA (elevada al padre de tarea): la instrucción de
--   diseño original mencionaba también «herrera» entre los que reciben una
--   ausencia que cubre hoy. Con la prioridad vacation → working de §5, una
--   ausencia aprobada de hoy convertiría a Herrera en vacation y rompería su
--   hecho de paridad «working hoy» (y la presencia de local_seed.sql y de
--   SyntheticWorkforceSource). Por eso SOLO 002 y 003 (y los generados)
--   reciben ausencias que cubren hoy; Herrera no recibe ninguna.
--
-- ORDEN DE CARGA (FK): employee → timeoff_type → working_day →
--   working_day_assigment → timeoff → time_check_delay → time_check.
-- ============================================================================

-- Fecha de referencia = día civil UTC durante toda la transacción.
SET TimeZone = 'UTC';

BEGIN;

-- ----------------------------------------------------------------------------
-- 1. employee — fixtures de paridad (ids 1..9) + 200 generados (ids 101..300).
--    Reparto de status: 12 eliminados (n%16=0, con deleted_on), 11 pendientes
--    (n%17=0), el resto activos (89% activo / 5% pendiente / 6% eliminado).
--    Cronología de cada generado: created (ene 2026) < updated (jun 2026) ≤
--    last_login (jun-ago 2026) < deleted_on (sept 2026) < hoy.
-- ----------------------------------------------------------------------------
INSERT INTO public.employee (
    id, created_on, deleted_on, first_email, phone, birthday_date, hire_date,
    name, department, last_name, last_login, language_id, role_id, status_id,
    "job:title", updated_on, vacancy_supervisor, timecheck_supervisor,
    photo_url, second_email)
VALUES
    (1, TIMESTAMP '2026-01-05 09:00:00', NULL, 'juan.perez@example.com', '+34600000001', TIMESTAMP '1985-04-12 00:00:00', TIMESTAMP '2019-02-11 00:00:00', 'Juan', 'office-demo-madrid', 'Pérez', TIMESTAMP '2026-10-16 17:42:00', 1, 1, 2, 'Responsable de equipo', TIMESTAMP '2026-09-10 10:00:00', 3, 3, '/photos/employee/juan-perez.png', 'juan.perez.alt@example.com'),
    (2, TIMESTAMP '2026-01-05 09:05:00', NULL, 'pedro.rojas@example.com', '+34600000002', TIMESTAMP '1990-07-23 00:00:00', TIMESTAMP '2020-06-01 00:00:00', 'Pedro', 'office-demo-tokyo', 'Rojas', TIMESTAMP '2026-10-16 09:20:00', 2, 1, 2, 'Especialista de soporte', TIMESTAMP '2026-08-21 12:30:00', 1, 1, '/photos/employee/pedro-rojas.png', ''),
    (3, TIMESTAMP '2026-01-05 09:10:00', NULL, 'mauricio.cardenas@example.com', '+34600000003', TIMESTAMP '1988-11-30 00:00:00', TIMESTAMP '2018-09-17 00:00:00', 'Mauricio', 'office-demo-madrid', 'Cardenas', TIMESTAMP '2026-10-15 18:05:00', 1, 0, 2, 'Técnico de nómina', TIMESTAMP '2026-07-03 09:45:00', 1, 1, NULL, ''),
    (4, TIMESTAMP '2026-01-05 09:15:00', NULL, 'juan.herrera@example.com', '+34600000004', TIMESTAMP '1992-02-18 00:00:00', TIMESTAMP '2021-03-22 00:00:00', 'Juan', 'office-demo-tokyo', 'Herrera', TIMESTAMP '2026-10-16 08:58:00', 2, 1, 2, 'Analista de jornada', TIMESTAMP '2026-09-01 16:20:00', 1, 1, '/photos/employee/juan-herrera.png', ''),
    (5, TIMESTAMP '2026-01-05 09:20:00', NULL, 'empleado.001@example.com', '+34600000005', NULL, TIMESTAMP '2022-01-10 00:00:00', 'Empleado', 'office-demo-madrid', 'sintético 001', TIMESTAMP '2026-10-16 07:55:00', 1, 0, 2, 'Operario de planta', NULL, 3, 3, NULL, ''),
    (6, TIMESTAMP '2026-01-05 09:25:00', NULL, 'empleado.005@example.com', '+34600000006', TIMESTAMP '1995-05-09 00:00:00', TIMESTAMP '2022-05-16 00:00:00', 'Empleado', 'office-demo-tokyo', 'sintético 005', TIMESTAMP '2026-10-16 08:12:00', 2, 0, 2, 'Operario de planta', TIMESTAMP '2026-06-14 11:00:00', 4, 4, '/photos/employee/empleado-005.png', ''),
    (7, TIMESTAMP '2026-01-05 09:30:00', NULL, 'empleado.004@example.com', '+34600000007', TIMESTAMP '1996-09-27 00:00:00', TIMESTAMP '2023-04-03 00:00:00', 'Empleado', 'office-demo-madrid', 'sintético 004', NULL, 1, 0, 2, NULL, NULL, 3, 3, NULL, ''),
    (8, TIMESTAMP '2026-01-05 09:35:00', NULL, 'empleado.002@example.com', '+34600000008', TIMESTAMP '1997-12-02 00:00:00', TIMESTAMP '2023-08-21 00:00:00', 'Empleado', 'office-demo-tokyo', 'sintético 002', TIMESTAMP '2026-10-16 09:05:00', 2, 0, 2, 'Mozo de almacén', TIMESTAMP '2026-05-20 13:10:00', 4, 4, NULL, ''),
    (9, TIMESTAMP '2026-01-05 09:40:00', NULL, 'empleado.003@example.com', '', TIMESTAMP '1998-03-14 00:00:00', TIMESTAMP '2024-11-05 00:00:00', 'Empleado', 'office-demo-madrid', 'sintético 003', NULL, 1, 0, 2, NULL, NULL, 3, 3, NULL, '')
;

-- Empleados generados: n = 1..200 → ids 101..300 (hueco permitido).
-- Departamentos humanos (10) + correos/teléfonos patrón con vacíos
-- ocasionales (los defaults del DDL), fotos/cumpleaños/último acceso a veces
-- NULL, supervisores apuntando a otros empleados existentes (0 en ~10%),
-- language_id mezclado 1/2 (NULL ocasional), role_id 0/1, "job:title" a
-- veces. hire_date repartido sobre 10 años (2016-2025).
INSERT INTO public.employee (
    id, created_on, deleted_on, first_email, phone, birthday_date, hire_date,
    name, department, last_name, last_login, language_id, role_id, status_id,
    "job:title", updated_on, vacancy_supervisor, timecheck_supervisor,
    photo_url, second_email)
SELECT
    100 + n,
    TIMESTAMP '2026-01-04 09:00:00' + (n * interval '11 minutes'),
    CASE WHEN n % 16 = 0 THEN TIMESTAMP '2026-09-01 10:00:00' + (n * interval '3 hours') ELSE NULL END,
    CASE WHEN n % 9 = 0 THEN '' ELSE 'persona' || n || '@example.com' END,
    CASE WHEN n % 7 = 0 THEN '' ELSE '+346' || lpad(((n * 7919) % 1000000000)::text, 9, '0') END,
    CASE WHEN n % 5 = 0 THEN NULL ELSE TIMESTAMP '1970-01-01 00:00:00' + ((n * 37) % 12000) * interval '1 day' END,
    TIMESTAMP '2016-01-01 00:00:00' + ((n * 19) % 3650) * interval '1 day',
    (ARRAY['Ana','Luis','Marta','Jorge','Lucía','Diego','Elena','Pablo','Sara','Iván','Carmen','Hugo','Nora','Álvaro','Raquel','Sergio','Alicia','Marcos','Irene','Rubén'])[1 + (n % 20)],
    (ARRAY['Ingeniería','Ventas','Marketing','Finanzas','Soporte','Operaciones','Personas','Legal','Calidad','Compras'])[1 + (n % 10)],
    (ARRAY['García','López','Martínez','Sánchez','Romero','Navarro','Iglesias','Ortega','Vázquez','Molina'])[1 + ((n * 7) % 20)],
    CASE WHEN n % 4 = 0 THEN NULL
         ELSE TIMESTAMP '2026-06-01 08:30:00' + ((n * 7) % 90) * interval '1 day' + ((n * 37) % 540) * interval '1 minute' END,
    CASE WHEN n % 11 = 0 THEN NULL ELSE 1 + (n % 2) END,
    CASE WHEN n % 3 = 0 THEN 1 ELSE 0 END,
    CASE WHEN n % 16 = 0 THEN 3 WHEN n % 17 = 0 THEN 1 ELSE 2 END,
    CASE WHEN n % 3 = 0 THEN 'Agente de nivel ' || (1 + (n % 4)) ELSE NULL END,
    CASE WHEN n % 2 = 0 THEN TIMESTAMP '2026-06-10 12:00:00' + (n * interval '5 minutes') ELSE NULL END,
    CASE WHEN n % 10 = 0 THEN 0 WHEN n % 2 = 0 THEN 1 + ((n * 3) % 9) ELSE 101 + ((n * 5) % 200) END,
    CASE WHEN n % 10 = 0 THEN 0 WHEN n % 3 = 0 THEN 1 + ((n * 5) % 9) ELSE 101 + ((n * 7) % 200) END,
    CASE WHEN n % 3 = 0 THEN NULL ELSE '/photos/employee/emp-' || n || '.png' END,
    CASE WHEN n % 3 = 0 THEN 'alternativo' || n || '@example.com' ELSE '' END
FROM generate_series(1, 200) AS g(n)
;

-- ----------------------------------------------------------------------------
-- 2. timeoff_type — catálogo de7 tipos (SPEC-012 §6): amounts distintos,
--    ventanas por fiscal_year 2025/2026 y un tipo inactivo (id 7, status 3).
--    Los tipos 1 y 2 llevan EXACTAMENTE los textos del contrato ('Vacación',
--    'Calamidad') porque sus ausencias de paridad se resuelven por esta FK.
-- ----------------------------------------------------------------------------
INSERT INTO public.timeoff_type (
    id, amount, date_start, date_end, name, status_id, fiscal_year, created_on)
VALUES
    (1, 0,  TIMESTAMP '2026-01-01 00:00:00', TIMESTAMP '2026-12-31 00:00:00', 'Vacación',          2, 2026, TIMESTAMP '2026-01-02 08:00:00'),
    (2, 0,  TIMESTAMP '2026-01-01 00:00:00', TIMESTAMP '2026-12-31 00:00:00', 'Calamidad',         2, 2026, TIMESTAMP '2026-01-02 09:00:00'),
    (3, 15, TIMESTAMP '2026-01-01 00:00:00', TIMESTAMP '2026-12-31 00:00:00', 'Enfermedad',        2, 2026, TIMESTAMP '2026-01-02 10:00:00'),
    (4, 5,  TIMESTAMP '2026-01-01 00:00:00', TIMESTAMP '2026-12-31 00:00:00', 'Permiso personal',  2, 2026, TIMESTAMP '2026-01-02 11:00:00'),
    (5, 3,  TIMESTAMP '2025-01-01 00:00:00', TIMESTAMP '2025-12-31 00:00:00', 'Permiso retribuido', 2, 2025, TIMESTAMP '2026-01-02 12:00:00'),
    (6, 2,  TIMESTAMP '2025-01-01 00:00:00', TIMESTAMP '2025-12-31 00:00:00', 'Hora compensada',   2, 2025, TIMESTAMP '2026-01-02 13:00:00'),
    (7, 10, NULL,                           NULL,                           'Formación externa', 3, 2025, TIMESTAMP '2026-01-02 14:00:00')
;

-- ----------------------------------------------------------------------------
-- 3. working_day — 7 jornadas; exactamente una es is_default (id 1, la
--    estándar 08:00-17:00); enable_delows y flags web/móvil variados.
-- ----------------------------------------------------------------------------
INSERT INTO public.working_day (
    id, status_id, name, description, created_on, modified_on,
    enable_delays, web_allowed, mobile_allowed, is_default)
SELECT
    wd.id, wd.status_id, wd.name, wd.description,
    TIMESTAMP '2026-01-05 08:00:00' + (wd.id * interval '1 hour'),
    TIMESTAMP '2026-03-02 09:00:00' + (wd.id * interval '1 hour'),
    wd.enable_delays, wd.web_allowed, wd.mobile_allowed, wd.is_default
FROM (VALUES
    (1, 2, 'Jornada estándar', 'De 08:00 a 17:00 con descanso de una hora', 1, true,  true,  true),
    (2, 2, 'Jornada partida',  'De 09:00 a 14:00 y de 15:00 a 18:00',      0, true,  true,  false),
    (3, 2, 'Turno mañana',     'De 06:00 a 14:00',                         1, true,  false, false),
    (4, 2, 'Turno tarde',      'De 14:00 a 22:00',                         1, false, true,  false),
    (5, 2, 'Turno noche',      'De 22:00 a 06:00',                         0, false, true,  false),
    (6, 1, 'Jornada reducida', 'De 08:00 a 15:00, treinta y cinco horas semanales', 1, true, true, false),
    (7, 2, 'Jornada flexible', 'Cuarenta horas semanales con entrada entre las 07:00 y las 09:30', 0, true, true, false)
) AS wd(id, status_id, name, description, enable_delays, web_allowed, mobile_allowed, is_default)
;

-- ----------------------------------------------------------------------------
-- 4. working_day_assigment — UNA asignación activa (status 2) por cada
--    empleado activo + 20 asignaciones históricas duplicadas (status 3).
--    La jornada asignada usa la MISMA fórmula que los marcajes para que
--    time_check.working_day_id siempre cuadre con la asignación:
--    fixtures (id < 100) → jornada 1; generados → 1 + (id % 7).
-- ----------------------------------------------------------------------------
INSERT INTO public.working_day_assigment (
    id, employee_id, working_day_id, status_id, created_on, modified_on)
SELECT
    row_number() OVER (ORDER BY e.id),
    e.id,
    CASE WHEN e.id < 100 THEN 1 ELSE 1 + (e.id % 7) END,
    2,
    TIMESTAMP '2026-01-06 08:00:00' + (row_number() OVER (ORDER BY e.id) * interval '7 minutes'),
    TIMESTAMP '2026-06-15 09:00:00' + (row_number() OVER (ORDER BY e.id) * interval '3 minutes')
FROM public.employee e
WHERE e.status_id = 2 AND e.deleted_on IS NULL
ORDER BY e.id
;

INSERT INTO public.working_day_assigment (
    id, employee_id, working_day_id, status_id, created_on, modified_on)
SELECT
    (SELECT max(id) FROM public.working_day_assigment) + row_number() OVER (ORDER BY e.id),
    e.id,
    1 + (e.id % 7),
    3,
    TIMESTAMP '2025-03-02 08:00:00' + (e.rn * interval '11 minutes'),
    TIMESTAMP '2025-11-20 10:00:00' + (e.rn * interval '5 minutes')
FROM (SELECT e.id, row_number() OVER (ORDER BY e.id) AS rn
      FROM public.employee e
      WHERE e.id BETWEEN 101 AND 300 AND e.status_id = 2 AND e.deleted_on IS NULL
      ORDER BY e.id
      LIMIT 20) e
;

-- ----------------------------------------------------------------------------
-- 5. timeoff — date_end INCLUSIVO (ver cabecera). 620 filas:
--    3 fixtures de paridad (textos sintéticos exactos en note), 2 ausencias
--    de hoy para 002/003 (paridad vacation), 10 ausencias aprobadas que
--    cubren hoy para empleados generados (casos §5) y 605 generadas con
--    ventanas pasadas/futuras/cruzando años fiscales 2025-2026, by_hour
--    sí/no, notas largas hasta 1000 caracteres, supervisor y fecha de
--    supervisión en la mayoría, ~5% pendientes (1), ~5% rechazadas (3).
-- ----------------------------------------------------------------------------
INSERT INTO public.timeoff (
    id, date_start, date_end, note, status_id, timeoff_type_id, employee_id,
    employee_id_supervisor, date_supervised, supervisor_note, fiscal_year,
    by_hour, created_on)
VALUES
    -- Paridad: mismos hechos que BuildTimeOff / local_seed.sql.
    (1, TIMESTAMP '2026-10-13 00:00:00', TIMESTAMP '2026-10-15 00:00:00', 'El HCM sintético informa una ausencia de vacaciones aprobada de Juan Pérez.', 2, 1, 1, 3, TIMESTAMP '2026-10-06 12:00:00', 'Aprobado por el responsable.', 2026, false, TIMESTAMP '2026-10-05 09:30:00'),
    (2, TIMESTAMP '2026-10-15 00:00:00', TIMESTAMP '2026-10-19 00:00:00', 'El HCM sintético informa la ausencia de vacaciones en curso de Pedro Rojas.', 2, 1, 2, 1, TIMESTAMP '2026-10-08 10:15:00', 'Aprobado por el responsable.', 2026, false, TIMESTAMP '2026-10-07 08:45:00'),
    (3, TIMESTAMP '2026-10-16 00:00:00', TIMESTAMP '2026-10-16 00:00:00', 'El HCM sintético informa una solicitud de calamidad rechazada de Mauricio Cardenas.', 3, 2, 3, 1, TIMESTAMP '2026-10-12 11:00:00', 'Rechazado por el responsable.', 2026, false, TIMESTAMP '2026-10-11 17:20:00'),
    -- Vacaciones aprobadas que cubren hoy para 002 (id 8) y 003 (id 9).
    (4, (CURRENT_DATE - 1)::timestamp, (CURRENT_DATE + 365)::timestamp, 'Vacaciones aprobadas que cubren la fecha de hoy (hecho de paridad de la semilla).', 2, 1, 8, 1, (CURRENT_DATE - 3)::timestamp + interval '10 hours', 'Aprobado por el responsable.', date_part('year', CURRENT_DATE)::integer, false, (CURRENT_DATE - 4)::timestamp + interval '9 hours'),
    (5, (CURRENT_DATE - 2)::timestamp, (CURRENT_DATE + 365)::timestamp, 'Vacaciones aprobadas que cubren la fecha de hoy (hecho de paridad de la semilla).', 2, 1, 9, 3, (CURRENT_DATE - 5)::timestamp + interval '11 hours', 'Aprobado por el responsable.', date_part('year', CURRENT_DATE)::integer, false, (CURRENT_DATE - 6)::timestamp + interval '9 hours')
;

-- 10 ausencias aprobadas que cubren hoy para empleados generados activos sin
-- marcaciones hoy (a%4=0 → not-arrived; con esta ausencia derivan vacation).
INSERT INTO public.timeoff (
    id, date_start, date_end, note, status_id, timeoff_type_id, employee_id,
    employee_id_supervisor, date_supervised, supervisor_note, fiscal_year,
    by_hour, created_on)
SELECT
    5 + row_number() OVER (ORDER BY e.id),
    (CURRENT_DATE - 2)::timestamp,
    (CURRENT_DATE + 365)::timestamp,
    'Vacaciones aprobadas que cubren la fecha de hoy (caso vacación generado para SPEC-012 §5).',
    2, 1, e.id, 1,
    (CURRENT_DATE - 5)::timestamp + interval '10 hours',
    'Aprobado por el responsable.',
    date_part('year', CURRENT_DATE)::integer,
    false,
    (CURRENT_DATE - 6)::timestamp + interval '9 hours'
FROM (SELECT id FROM public.employee
      WHERE id BETWEEN 101 AND 300 AND status_id = 2 AND deleted_on IS NULL AND id % 4 = 0
      ORDER BY id LIMIT 10) e
;

-- 605 ausencias generadas (ids 16..620). date_start repartido sobre
-- 2025-01-01..2026-12-31 (pasado, futuro y cruzando años fiscales);
-- date_end = start + 0..9 días o NULL ("hasta nuevo aviso", §10.5).
INSERT INTO public.timeoff (
    id, date_start, date_end, note, status_id, timeoff_type_id, employee_id,
    employee_id_supervisor, date_supervised, supervisor_note, fiscal_year,
    by_hour, created_on)
SELECT
    n,
    TIMESTAMP '2025-01-01 00:00:00' + ((n * 17) % 730) * interval '1 day',
    CASE WHEN n % 13 = 0 THEN NULL
         ELSE TIMESTAMP '2025-01-01 00:00:00' + (((n * 17) % 730) + ((n * 3) % 10)) * interval '1 day' END,
    CASE WHEN n % 11 = 0 THEN NULL
         WHEN n % 7 = 0 THEN left(repeat('Solicitud de ausencia generada con nota larga para robustez del contrato. ', 30), 1000)
         ELSE 'Solicitud de ausencia generada ' || n END,
    CASE WHEN n % 20 = 0 THEN 3 WHEN n % 17 = 0 THEN 1 ELSE 2 END,
    1 + (n % 6),
    101 + ((n * 13) % 200),
    CASE WHEN n % 11 = 0 THEN NULL ELSE 101 + ((n * 29) % 200) END,
    CASE WHEN n % 11 = 0 THEN NULL
         ELSE TIMESTAMP '2025-01-01 00:00:00' + (((n * 17) % 730) + ((n * 3) % 10) + 2) * interval '1 day' + interval '10 hours' END,
    CASE WHEN n % 5 = 0 THEN NULL ELSE 'Revisión de la ausencia por el responsable.' END,
    CASE WHEN ((n * 17) % 730) < 365 THEN 2025 ELSE 2026 END,
    (n % 2 = 0),
    TIMESTAMP '2025-01-01 00:00:00' + (((n * 17) % 730) - (((n * 5) % 30) + 5)) * interval '1 day' + interval '10 hours'
FROM generate_series(16, 620) AS g(n)
;

-- ----------------------------------------------------------------------------
-- 6. time_check_delay — 60 incidencias (ids 1..60) sobre los primeros 60
--    empleados generados activos; estados mezclados (2 aprobada / 1
--    pendiente), comentarios largos hasta 1000 caracteres, approved_by sobre
--    empleados existentes. created_on = día laborable anterior al de los
--    marcajes enlazados (ver k_link en time_check).
-- ----------------------------------------------------------------------------
INSERT INTO public.time_check_delay (
    id, comments, status_id, created_on, modified_on, approved_by, employee_id)
SELECT
    d.rn,
    CASE WHEN d.rn % 4 = 0
         THEN left(repeat('Comentario largo de la incidencia de retraso para robustez del contrato. ', 25), 1000)
         ELSE 'Retraso justificado por tráfico matinal.' END,
    CASE WHEN d.rn % 3 = 0 THEN 1 ELSE 2 END,
    (CURRENT_DATE - (CASE WHEN extract(isodow FROM CURRENT_DATE) = 1 THEN 3 ELSE 1 END))::timestamp
        + TIME '09:15:00' + (d.rn * interval '1 minute'),
    (CURRENT_DATE - (CASE WHEN extract(isodow FROM CURRENT_DATE) = 1 THEN 3 ELSE 1 END))::timestamp
        + TIME '09:35:00' + (d.rn * interval '1 minute'),
    1 + (d.rn % 9),
    d.employee_id
FROM (SELECT row_number() OVER (ORDER BY e.id) AS rn, e.id AS employee_id
      FROM public.employee e
      WHERE e.id BETWEEN 101 AND 300 AND e.status_id = 2 AND e.deleted_on IS NULL
      ORDER BY e.id
      LIMIT 60) d
;

-- ----------------------------------------------------------------------------
-- 7. time_check — marcaciones. Empleados generados activos, ~45 días
--    (k = 0 hoy .. 44) con patrón realista por día laborable:
--      slot 0 entrada        (check_type_id=1, checkout_type_id=0)
--      slot 1 pausa fuera    (check_type_id=2, checkout_type_id=1)
--      slot 2 pausa vuelta   (check_type_id=1, checkout_type_id=0)
--      slot 3 almuerzo fuera (check_type_id=2, checkout_type_id=2)
--      slot 4 almuerzo vuelta(check_type_id=1, checkout_type_id=0)
--      slot 5 fin de jornada (check_type_id=2, checkout_type_id=3)
--    Ausencias ocasionales (1 día de cada 9), 20% de días pasados sin cierre.
--    HOY (k=0): a%4=1 → solo entrada (working); a%4=0 → sin marcaciones
--    (not-arrived); a%4=2/3 → día completo con fin de jornada (absent).
--    k_link (día laborable anterior) enlaza exactamente 60 entradas con las
--    incidencias del punto 6; el resto de delay_id queda en el centinela 0.
--    Solo los fixtures tienen reglas propias y se insertan aparte (ids 1..4).
-- ----------------------------------------------------------------------------
INSERT INTO public.time_check (
    id, employee_id, working_day_id, check_type_id, checkout_type_id,
    time_check_date_utc, time_check_date_long, time_check_timezone,
    check_device_type_id, status_id, supervisor_status_date, employee_note,
    longitude, latitude, ip_address, created_on, modified_on, delay_id,
    photo_face_url, photo_face_status_id)
WITH demo(id) AS (VALUES (1), (4), (5), (6)),
punch AS (
    SELECT e.id,
           (e.department = 'office-demo-tokyo') AS tokyo,
           row_number() OVER (ORDER BY e.id) AS rn,
           (CURRENT_DATE
             + (CASE WHEN e.department = 'office-demo-tokyo' THEN 540 ELSE 480 END + (e.id * 7) % 75) * interval '1 minute'
             - (CASE WHEN e.department = 'office-demo-tokyo' THEN 540 ELSE 120 END) * interval '1 minute') AS utc_wall
    FROM demo d JOIN public.employee e ON e.id = d.id
)
SELECT
    p.rn,
    p.id,
    1,
    1,
    0,
    p.utc_wall AT TIME ZONE 'UTC',
    (extract(epoch FROM (p.utc_wall AT TIME ZONE 'UTC')))::bigint * 1000,
    CASE WHEN p.tokyo THEN 'Asia/Tokyo' ELSE 'Europe/Madrid' END,
    1 + (p.id % 3),
    2,
    p.utc_wall + interval '5 minutes',
    NULL,
    round((CASE WHEN p.tokyo THEN 139.6917 ELSE -3.7038 END + (((p.id * 13) % 9999)::double precision / 1000000.0))::numeric, 6)::double precision,
    round((CASE WHEN p.tokyo THEN 35.6895 ELSE 40.4168 END + (((p.id * 7) % 9999)::double precision / 1000000.0))::numeric, 6)::double precision,
    '192.168.1.' || p.id,
    p.utc_wall,
    p.utc_wall + interval '30 seconds',
    0,
    '/faces/demo-face-' || p.id || '.jpg',
    1
FROM punch p
;

-- Marcaciones de los empleados generados activos (~45 días incluyendo hoy).
-- Cada fila lleva created_on/modified_on/supervisor_status_date explícitos
-- (nunca el DEFAULT CURRENT_TIMESTAMP) para que la carga sea determinista.
INSERT INTO public.time_check (
    id, employee_id, working_day_id, check_type_id, checkout_type_id,
    time_check_date_utc, time_check_date_long, time_check_timezone,
    check_device_type_id, status_id, supervisor_status_date, employee_note,
    longitude, latitude, ip_address, created_on, modified_on, delay_id,
    photo_face_url, photo_face_status_id)
WITH klink AS (
    SELECT CASE WHEN extract(isodow FROM CURRENT_DATE) = 1 THEN 3 ELSE 1 END AS k_link
),
days AS (
    SELECT k, CURRENT_DATE - k AS d
    FROM generate_series(0, 44) AS g(k)
),
pool AS (
    SELECT id AS a, (id % 3 = 0) AS tokyo
    FROM public.employee
    WHERE id BETWEEN 101 AND 300 AND status_id = 2 AND deleted_on IS NULL
),
cells AS (
    SELECT p.a, p.tokyo, dk.k, dk.d, klink.k_link,
           CASE WHEN dk.k = 0 THEN p.a % 4 ELSE -1 END AS r
    FROM pool p
    CROSS JOIN days dk
    CROSS JOIN klink
    WHERE CASE WHEN dk.k = 0 THEN (p.a % 4) <> 0
               ELSE extract(isodow FROM dk.d) <= 5
                    AND (dk.k = klink.k_link OR ((p.a + dk.k) % 9) >= 2)
          END
),
slots AS (
    SELECT c.*, g.slot,
           CASE WHEN g.slot = 5
                THEN (CASE WHEN c.tokyo THEN 1080 ELSE 1020 END)
                     + (((c.a * 11 + c.k * 5) % 45))
                ELSE (CASE WHEN c.tokyo THEN 540 ELSE 480 END)
                     + (((c.a * 7 + c.k * 13) % 75))
                     + CASE g.slot
                           WHEN 1 THEN 130 + (c.a % 10)
                           WHEN 2 THEN 145 + (c.a % 10) + (c.k % 5)
                           WHEN 3 THEN 265 + (c.a % 15)
                           WHEN 4 THEN 310 + (c.a % 15)
                           ELSE 0
                       END
           END AS local_min
    FROM cells c
    CROSS JOIN LATERAL generate_series(0, 5) AS g(slot)
    WHERE g.slot = 0
       OR ((c.k > 0 OR c.r <> 1) AND (
            (g.slot IN (1, 2) AND ((c.a + c.k) % 2) = 0)
         OR (g.slot IN (3, 4) AND ((c.a + c.k) % 3) <> 0)
         OR (g.slot = 5 AND (c.k = 0 OR ((c.a + c.k) % 5) <> 0))
       ))
),
punches AS (
    SELECT s.*,
           (s.d + s.local_min * interval '1 minute'
             - (CASE WHEN s.tokyo THEN 540 ELSE 120 END) * interval '1 minute') AS utc_wall,
           CASE WHEN s.tokyo THEN 'Asia/Tokyo' ELSE 'Europe/Madrid' END AS tz
    FROM slots s
)
SELECT
    4 + row_number() OVER (ORDER BY p.a, p.k, p.slot),
    p.a,
    1 + (p.a % 7),
    CASE WHEN p.slot IN (0, 2, 4) THEN 1 ELSE 2 END,
    CASE p.slot WHEN 0 THEN 0 WHEN 1 THEN 1 WHEN 2 THEN 0 WHEN 3 THEN 2 WHEN 4 THEN 0 ELSE 3 END,
    p.utc_wall AT TIME ZONE 'UTC',
    (extract(epoch FROM (p.utc_wall AT TIME ZONE 'UTC')))::bigint * 1000,
    p.tz,
    1 + ((p.a + p.k + p.slot) % 3),
    CASE WHEN ((p.a * 7 + p.k * 3 + p.slot) % 23) = 0 THEN 3
         WHEN ((p.a * 7 + p.k * 3 + p.slot) % 17) = 0 THEN 1
         ELSE 2 END,
    p.utc_wall + interval '5 minutes',
    CASE WHEN ((p.a + p.k) % 19) = 0
         THEN left(repeat('Marcaje con nota larga del empleado para robustez del contrato. ', 30), 1000)
         ELSE NULL END,
    round((CASE WHEN p.tokyo THEN 139.6917 ELSE -3.7038 END
            + (((p.a * 13 + p.k) % 9999)::double precision / 1000000.0))::numeric, 6)::double precision,
    round((CASE WHEN p.tokyo THEN 35.6895 ELSE 40.4168 END
            + (((p.a * 7 + p.k) % 9999)::double precision / 1000000.0))::numeric, 6)::double precision,
    CASE WHEN p.tokyo THEN '192.168.' || ((p.a + p.k) % 250) || '.' || ((p.a * 3 + p.slot) % 250)
         ELSE '10.' || (p.a % 200) || '.' || ((p.k * 3) % 250) || '.' || ((p.a + p.k + p.slot) % 250) END,
    p.utc_wall,
    p.utc_wall + interval '30 seconds',
    CASE WHEN p.k = p.k_link AND p.slot = 0 THEN COALESCE(td.id, 0) ELSE 0 END,
    CASE WHEN ((p.a + p.k + p.slot * 3) % 10) < 7
         THEN '/faces/face-' || p.a || '-' || p.k || '-' || p.slot || '.jpg'
         ELSE NULL END,
    CASE WHEN ((p.a + p.k + p.slot * 3) % 10) < 7
         THEN CASE WHEN ((p.a + p.k + p.slot) % 8) = 0 THEN 2 ELSE 1 END
         ELSE 0 END
FROM punches p
LEFT JOIN public.time_check_delay td
       ON td.employee_id = p.a AND p.k = p.k_link
ORDER BY p.a, p.k, p.slot
;

COMMIT;
