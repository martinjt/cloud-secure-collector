FROM ghcr.io/martinjt/ocb-config-builder:latest as build
COPY config.yaml /config/config.yaml
RUN /builder/build-collector.sh /config/config.yaml
 
FROM cgr.dev/chainguard/static:latest
COPY --from=build /app/otelcol-custom /
COPY config.yaml /
EXPOSE 4317/tcp 4318/tcp 13133/tcp
 
CMD ["/otelcol-custom", "--config=/config.yaml"]