# Estado funcional de OceanFixture

Actualizado: 17/10/2026. Este documento describe lo implementado, no promete una replica terminada.

## Objetivo

Recrear el flujo de gestion de embarques de la plataforma original, conservando su estructura visual con la marca OceanFixture: configurar perfil, crear cargas, comparar ofertas, negociar, cerrar el Recap y controlar el embarque. Se avanza por partes, con pruebas y commits verificables.

## Estado por modulo

| Modulo | Disponible | Limites pendientes |
| --- | --- | --- |
| Dashboard | Operaciones por etapa, busqueda por texto y acceso segun empresa/rol | Filtros avanzados, avisos y actualizacion en tiempo real |
| Mapa | Cartografia real, desplazamiento, zoom y plegado | No representa todavia embarques, barcos ni recorridos |
| Alta de carga | Tres pasos, catalogo de armadores, validacion, referencia del servidor y persistencia | No envia correos; borradores pendientes |
| Ofertas del armador | Envio y actualizacion de la propia oferta para una carga abierta e invitada | Solo se conserva la oferta vigente; faltan historial, declinacion y fechas limite |
| Comparativa | Datos de la API, diferencias entre propuestas y seleccion del armador | Recordatorios, cancelacion, archivo, chat y notas |
| Negociacion | Condiciones, clausulas propias, contraofertas, control de version, turnos obligatorios e historial persistente | Aceptacion por campo, borradores y auditoria inalterable |
| Recap | Doble confirmacion de la misma version, firmante de la sesion y conservacion tras reinicio | Identidad verificada, firma contractual de produccion, comision y auditoria inalterable |
| Roles | Permisos de cargador, armador y administrador aplicados en API e interfaz | Administracion completa de usuarios, empresas y membresias |
| Persistencia | SQLite local con transacciones y recuperacion ante fallo de escritura | Modelo relacional normalizado, migraciones, varias instancias y copias automaticas |
| Comunicaciones y adjuntos | Servicios auxiliares preliminares | Chat, notas, correos y almacenamiento de documentos operativo |
| Operativa | Etapa representada en el flujo | Pantalla completa: buque/IMO, puertos, ETA/ATA/ETD/ATD, escalas y seguimiento |
| Acceso de produccion | No disponible | Login real, recuperacion de cuenta, identidad verificada y despliegue seguro |

## Flujos y privacidad por rol

- El cargador solo consulta sus operaciones, invita armadores, compara propuestas, elige la contraparte, negocia y confirma su parte.
- El armador ve las cargas a las que esta invitado mientras estan abiertas. Puede enviar o actualizar su propuesta, sin leer propuestas ni invitaciones de competidores. Tras la seleccion solo accede a operaciones donde sea el armador elegido.
- El administrador consulta el conjunto y puede gestionar altas/seleccion. No se hace pasar por las dos partes para confirmar un acuerdo. El panel administrativo completo aun no existe.
- La sesion demo permite probar estos roles, pero cualquier usuario local autorizado a usar el selector puede escoger un perfil. Por ello no equivale a autenticar personas reales.

## Respuesta del armador

Se entra por `Submit offer` / `Update my offer` en la tarjeta o comparativa. La pantalla muestra las condiciones originales y la propuesta propia. El servidor asigna empresa, nombre del armador, identificador y fecha de recepcion; no acepta elegir otro armador desde el formulario.

Se exige carga abierta, invitacion, empresa aprobada, cantidad y flete positivos, moneda de tres letras, unidades, mercancia, puertos y laycan. Actualizar sustituye la propuesta vigente de ese armador, conserva su identificador y no altera la solicitud original ni las propuestas de otros armadores. Seleccionar una oferta cierra esta fase y lleva sus condiciones a negociacion.

La version es global a la operacion: otra respuesta puede hacer que un formulario abierto devuelva 409. En ese caso no se guarda y se conservan los cambios escritos. Recargar es una accion explicita que descarta esos cambios. Esta primera pantalla reutiliza los estilos de la maqueta; su fidelidad exacta al formulario original del armador queda pendiente de contrastar.

## Turnos e historial de negociacion

Seleccionar una oferta firm deja el primer turno al armador elegido. Cada contraoferta aceptada alterna el turno; solo el cargador propietario o el armador seleccionado pueden enviar cuando les corresponde. El administrador consulta sin negociar por las partes. El servidor comprueba permisos, version y turno; el turno se vuelve a comprobar bajo el bloqueo de escritura. En espera, la pantalla muestra los datos en lectura y permite refrescar. La confirmacion del Recap no exige tener el turno: cada parte puede aceptar la version vigente.

Cada nueva contraoferta guarda numero, autor de la sesion, parte, fecha UTC y una copia de todas las condiciones y clausulas anteriores/enviadas. El historial desplegable muestra los valores y resalta las condiciones modificadas. Se conserva en SQLite y se revierte junto con la operacion si falla la escritura. No se reconstruyen acciones demo antiguas ni revisiones previas de propuestas iniciales; tampoco hay todavia aceptacion por campo. Es un registro local, no una auditoria a prueba de manipulaciones. Anadir el historial no invalida confirmaciones existentes.

## Seguimiento maritimo: alcance acordado

Las referencias aportadas por el usuario muestran un mapa asociado al embarque, un panel del buque y una pantalla Operative con comunicaciones y Recap. No debe considerarse terminado con el simple mapa geografico.

Quedan por integrar posicion y ultimo reporte del buque, velocidad, rumbo, calado, estado, trazado del recorrido, escalas, meteorologia y datos del viaje. Hay que distinguir recorrido observado, ruta prevista y segmentos sin cobertura; las capturas no confirman por si solas el significado de cada color.

El usuario recuerda VesselFinder como fuente original, pero admite otro proveedor. No hay proveedor contratado ni decidido: se evaluara cobertura, licencia, costes y disponibilidad por tipo de dato cuando se aborde esta parte. No se mostraran posiciones de demostracion como si fueran datos reales.

## Referencias y documentacion

- [README](../README.md): arranque, arquitectura, endpoints y restricciones operativas.
- [Registro de desarrollo](REGISTRO_DE_DESARROLLO.md): avances y decisiones.
- [Pruebas manuales](PRUEBAS_MANUALES.md): recorridos reproducibles entre perfiles.
- Analisis funcional inicial: `CLH/CONOCIMIENTO_FUNCIONAL_CARGO_LIGHTHOUSE.md`, fuera de este repositorio.
- Capturas locales: fuera del repositorio, segun la peticion del usuario. No se incorporan credenciales, tokens, bases de datos ni capturas privadas a Git.

El entorno original no respondio al intento de revision del 16/10/2026. Las zonas no inspeccionadas, especialmente el panel administrativo, no se documentan como verificadas.

## Siguientes incrementos

Completar aceptacion por campo y borradores de negociacion, administracion/perfiles y clausulas reutilizables; despues comunicaciones/documentos y operativa del embarque. La identidad de produccion, el modelo de datos definitivo y la comprobacion visual siguen siendo tareas transversales. El orden puede ajustarse segun dependencias y referencias disponibles.
