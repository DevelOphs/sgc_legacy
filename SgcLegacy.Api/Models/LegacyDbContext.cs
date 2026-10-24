using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace SgcLegacy.Api.Models;

public partial class LegacyDbContext : DbContext
{
    public LegacyDbContext(DbContextOptions<LegacyDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Cliente> Clientes { get; set; }

    public virtual DbSet<Comprobante> Comprobantes { get; set; }

    public virtual DbSet<DetallePedido> DetallePedidos { get; set; }

    public virtual DbSet<Inventario> Inventarios { get; set; }

    public virtual DbSet<MovimientoInventario> MovimientoInventarios { get; set; }

    public virtual DbSet<Notificacion> Notificacions { get; set; }

    public virtual DbSet<Pedido> Pedidos { get; set; }

    public virtual DbSet<Producto> Productos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("cliente_pkey");

            entity.ToTable("cliente");

            entity.HasIndex(e => e.Email, "cliente_email_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .HasColumnName("email");
            entity.Property(e => e.Nombre)
                .HasMaxLength(150)
                .HasColumnName("nombre");
        });

        modelBuilder.Entity<Comprobante>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("comprobante_pkey");

            entity.ToTable("comprobante");

            entity.HasIndex(e => e.Codigoreferencia, "comprobante_codigoreferencia_key").IsUnique();

            entity.HasIndex(e => e.Pedidoid, "comprobante_pedidoid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Codigoreferencia)
                .HasMaxLength(50)
                .HasColumnName("codigoreferencia");
            entity.Property(e => e.Fechaemision)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("fechaemision");
            entity.Property(e => e.Pedidoid).HasColumnName("pedidoid");

            entity.HasOne(d => d.Pedido).WithOne(p => p.Comprobante)
                .HasForeignKey<Comprobante>(d => d.Pedidoid)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_comprobante_pedido");
        });

        modelBuilder.Entity<DetallePedido>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("detalle_pedido_pkey");

            entity.ToTable("detalle_pedido");

            entity.HasIndex(e => e.Pedidoid, "idx_detalle_pedido");

            entity.HasIndex(e => e.Productoid, "idx_detalle_producto");

            entity.HasIndex(e => new { e.Pedidoid, e.Productoid }, "uq_pedido_producto").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Cantidad).HasColumnName("cantidad");
            entity.Property(e => e.Pedidoid).HasColumnName("pedidoid");
            entity.Property(e => e.Preciounitario)
                .HasPrecision(12, 2)
                .HasColumnName("preciounitario");
            entity.Property(e => e.Productoid).HasColumnName("productoid");

            entity.HasOne(d => d.Pedido).WithMany(p => p.DetallePedidos)
                .HasForeignKey(d => d.Pedidoid)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_detalle_pedido");

            entity.HasOne(d => d.Producto).WithMany(p => p.DetallePedidos)
                .HasForeignKey(d => d.Productoid)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_detalle_producto");
        });

        modelBuilder.Entity<Inventario>(entity =>
        {
            entity.HasKey(e => e.Productoid).HasName("inventario_pkey");

            entity.ToTable("inventario");

            entity.Property(e => e.Productoid)
                .ValueGeneratedNever()
                .HasColumnName("productoid");
            entity.Property(e => e.Stockdisponible).HasColumnName("stockdisponible");
            entity.Property(e => e.Ultimaactualizacion)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("ultimaactualizacion");

            entity.HasOne(d => d.Producto).WithOne(p => p.Inventario)
                .HasForeignKey<Inventario>(d => d.Productoid)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_inventario_producto");
        });

        modelBuilder.Entity<MovimientoInventario>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("movimiento_inventario_pkey");

            entity.ToTable("movimiento_inventario");

            entity.HasIndex(e => e.Pedidoid, "idx_movimiento_pedido");

            entity.HasIndex(e => e.Productoid, "idx_movimiento_producto");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Cantidad).HasColumnName("cantidad");
            entity.Property(e => e.Fechamovimiento)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("fechamovimiento");
            entity.Property(e => e.Pedidoid).HasColumnName("pedidoid");
            entity.Property(e => e.Productoid).HasColumnName("productoid");
            entity.Property(e => e.Tipomovimiento)
                .HasMaxLength(20)
                .HasColumnName("tipomovimiento");

            entity.HasOne(d => d.Pedido).WithMany(p => p.MovimientoInventarios)
                .HasForeignKey(d => d.Pedidoid)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_movimiento_pedido");

            entity.HasOne(d => d.Producto).WithMany(p => p.MovimientoInventarios)
                .HasForeignKey(d => d.Productoid)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_movimiento_producto");
        });

        modelBuilder.Entity<Notificacion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notificacion_pkey");

            entity.ToTable("notificacion");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Comprobanteid).HasColumnName("comprobanteid");
            entity.Property(e => e.Emaildestino)
                .HasMaxLength(150)
                .HasColumnName("emaildestino");
            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .HasDefaultValueSql("'ENVIADO'::character varying")
                .HasColumnName("estado");
            entity.Property(e => e.Fechaintento)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("fechaintento");

            entity.HasOne(d => d.Comprobante).WithMany(p => p.Notificacions)
                .HasForeignKey(d => d.Comprobanteid)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_notificacion_comprobante");
        });

        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pedido_pkey");

            entity.ToTable("pedido");

            entity.HasIndex(e => e.Clienteid, "idx_pedido_cliente");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Clienteid).HasColumnName("clienteid");
            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .HasColumnName("estado");
            entity.Property(e => e.Fechaconfirmacion).HasColumnName("fechaconfirmacion");
            entity.Property(e => e.Fechacreacion)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("fechacreacion");

            entity.HasOne(d => d.Cliente).WithMany(p => p.Pedidos)
                .HasForeignKey(d => d.Clienteid)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pedido_cliente");
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("producto_pkey");

            entity.ToTable("producto");

            entity.HasIndex(e => e.Codigosku, "producto_codigosku_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Codigosku)
                .HasMaxLength(50)
                .HasColumnName("codigosku");
            entity.Property(e => e.Nombre)
                .HasMaxLength(150)
                .HasColumnName("nombre");
            entity.Property(e => e.Preciobase)
                .HasPrecision(12, 2)
                .HasColumnName("preciobase");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
