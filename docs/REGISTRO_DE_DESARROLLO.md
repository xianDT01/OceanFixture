# Registro de desarrollo y decisiones

Este registro resume hitos; el historial exacto de archivos y cambios se conserva en Git. No sustituye una auditoria de acciones de usuarios.

## Base del proyecto

OceanFixture usa Angular 17 y ASP.NET Core 8, con servicios separados para API principal, identidad, notificaciones y almacenamiento. Se conserva el estilo de la maqueta y se cambia la marca. Los servicios auxiliares son preliminares, no funciones completas de produccion.

## Recap, negociacion y comparativa

- `c9cf022`: Recap conectado a la API, doble confirmacion por version, rechazo de versiones antiguas y anulacion de confirmaciones al cambiar condiciones.
- `e44adf9`: negociacion conectada a la API, guardado de condiciones/clausulas, proteccion frente a envios repetidos y conservacion del formulario ante errores.
- `21915ca`: comparativa con ofertas del servidor y seleccion real del armador. La referencia demo 2941 comienza como carga abierta, no como viaje ya cerrado.
- `69f9bb1`: alta de cargas en tres pasos conectada al catalogo de empresas y a la API. Las referencias las asigna el servidor. Guardar destinatarios no significa enviar correos.

## 16/10/2026: mapa geografico

- `53893d8`: sustitucion del dibujo por Leaflet/OpenStreetMap, zoom, desplazamiento, plegado, atribucion y aviso de error de carga.
- Dashboard de carga diferida para que la libreria cartografica no incremente el paquete inicial de todas las pantallas.
- Las capturas posteriores del usuario aclaran que el objetivo final incluye seguimiento y operativa del buque. El mapa geografico no cubre ese alcance.
- La fuente de seguimiento podria ser VesselFinder u otra alternativa. No se ha contratado ni integrado un proveedor de AIS.

## 17/10/2026: persistencia

- `4e77aff`: SQLite local, guardado transaccional y restauracion del estado en memoria si falla la escritura. La API devuelve 503 en vez de anunciar un exito no guardado.
- Decidido usar inicialmente una instantanea JSON en una tabla SQLite para conservar el modelo actual sin redisenar todo el dominio en un incremento.
- Limite deliberado: una sola instancia local, protegida por un bloqueo exclusivo. Normalizacion, migraciones y varias instancias quedan pendientes.
- Las bases corruptas o de esquema desconocido no se sustituyen automaticamente con datos demo.
- Verificado un reinicio real y fallos de escritura; bases locales excluidas de Git.

## 17/10/2026: primera separacion de roles

- `a00acc3`: sesiones demo locales con un rol y empresa por token, acceso por empresa, ocultacion de ofertas competidoras y permisos en servidor e interfaz.
- Deshabilitado el antiguo login auxiliar que aceptaba cualquier contrasena y devolvia todos los roles. La demo local no se presenta como identidad de produccion.
- El firmante del Recap procede de la sesion; el administrador no confirma como ambas partes. Las clausulas de la contraparte no son editables por el otro rol.
- El endpoint antiguo de contraoferta devuelve 410 por HTTP; se usa el flujo con control de version.
- Se preserva la version completa al ocultar datos en las respuestas del armador. Esto corrige un conflicto detectado en la prueba HTTP entre privacidad y control de concurrencia.
- Verificados 72 tests backend y 51 frontend en ese hito, ademas del recorrido HTTP por roles. Estos numeros son una fotografia del hito, no cobertura total de la plataforma.

## 17/10/2026: ofertas del armador y documentacion funcional

- Nuevo envio/actualizacion de propuesta propia desde una carga invitada y abierta. El cargador la recibe en la comparativa y puede seleccionarla para negociar.
- Una propuesta vigente por armador y carga: actualizar no duplica columnas ni modifica las propuestas de otros. El historial de revisiones es una tarea posterior.
- Empresa, nombre, fecha e identificador se asignan en el servidor; se rechaza la suplantacion de otro armador.
- Validacion, control de version y transaccion SQLite tambien en este flujo. Los errores no se presentan como envios correctos.
- Se incorporan documentos separados de estado funcional y pruebas manuales, enlazados desde el README. En cada incremento se actualizaran las funciones afectadas, limites y verificaciones realizadas.
- Verificacion del hito: 97 tests backend, 60 frontend, compilaciones .NET/Angular y prueba HTTP de carga nueva -> dos propuestas independientes -> actualizacion propia -> seleccion -> Recap confirmado por ambas partes. Hay pruebas de persistencia y fallo de escritura para las propuestas. No equivale a validar todas las pantallas originales ni el uso de produccion.
